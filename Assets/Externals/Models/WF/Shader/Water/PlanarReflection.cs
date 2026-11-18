using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class PlanarReflection : MonoBehaviour
{
    [Header("Camera")]
    public Camera sourceCamera;
    public LayerMask reflectionLayers = -1;

    [Header("Performance")]
    [Range(0.125f, 1f)]
    public float resolutionScale = 0.5f;

    [Min(1)]
    public int updateInterval = 2;

    public bool renderShadows = false;
    public bool useHDR = false;

    [Header("Water")]
    public float clipPlaneOffset = 0.05f;
    public bool flipReflectionY = false;

    [Header("Shader")]
    public string reflectionTextureName = "_PlanarReflectionTexture";
    public string reflectionMatrixName = "_ReflectionVP";
    public string reflectionYFlipName = "_ReflectionYFlip";

    Renderer waterRenderer;
    Camera reflectionCamera;
    RenderTexture reflectionTexture;
    MaterialPropertyBlock propertyBlock;

    int frameCounter;
    int textureWidth;
    int textureHeight;

    static readonly int ReflectionTextureID =
        Shader.PropertyToID("_PlanarReflectionTexture");

    void OnEnable()
    {
        waterRenderer = GetComponent<Renderer>();

        if (sourceCamera == null)
            sourceCamera = Camera.main;

        propertyBlock = new MaterialPropertyBlock();

        CreateReflectionCamera();
    }

    void LateUpdate()
    {
        if (sourceCamera == null || reflectionCamera == null)
            return;

        frameCounter++;

        if (frameCounter % Mathf.Max(1, updateInterval) != 0)
            return;

        UpdateReflection();
    }

    void CreateReflectionCamera()
    {
        GameObject go = new GameObject("Planar Reflection Camera");
        go.hideFlags = HideFlags.HideAndDontSave;

        reflectionCamera = go.AddComponent<Camera>();
        reflectionCamera.enabled = false;

        UniversalAdditionalCameraData data =
            go.AddComponent<UniversalAdditionalCameraData>();

        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;

        reflectionCamera.useOcclusionCulling = false;
        reflectionCamera.allowMSAA = false;
    }

    void UpdateReflection()
    {
        int width = Mathf.Max(
            16,
            Mathf.RoundToInt(sourceCamera.pixelWidth * resolutionScale));

        int height = Mathf.Max(
            16,
            Mathf.RoundToInt(sourceCamera.pixelHeight * resolutionScale));

        if (reflectionTexture == null ||
            width != textureWidth ||
            height != textureHeight)
        {
            CreateRenderTexture(width, height);
        }

        reflectionCamera.CopyFrom(sourceCamera);
        reflectionCamera.enabled = false;
        reflectionCamera.targetTexture = reflectionTexture;

        reflectionCamera.cullingMask =
            sourceCamera.cullingMask & reflectionLayers.value;

        reflectionCamera.useOcclusionCulling = false;
        reflectionCamera.allowMSAA = false;
        reflectionCamera.allowHDR = useHDR;

        reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
        reflectionCamera.backgroundColor = Color.black;

        reflectionCamera.depthTextureMode = DepthTextureMode.None;

        // Reflection plane
        Vector3 normal = transform.up.normalized;
        Vector3 position = transform.position;

        Vector4 plane = new Vector4(
            normal.x,
            normal.y,
            normal.z,
            -Vector3.Dot(normal, position) - clipPlaneOffset
        );

        Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(plane);

        Vector3 reflectedPosition =
            reflectionMatrix.MultiplyPoint(sourceCamera.transform.position);

        reflectionCamera.transform.position = reflectedPosition;

        // Reflected view matrix
        reflectionCamera.worldToCameraMatrix =
            sourceCamera.worldToCameraMatrix * reflectionMatrix;

        // Oblique clipping
        Vector4 cameraSpacePlane =
            CameraSpacePlane(
                reflectionCamera,
                position,
                normal,
                clipPlaneOffset
            );

        reflectionCamera.projectionMatrix =
            sourceCamera.CalculateObliqueMatrix(cameraSpacePlane);

        // Reverse face culling because the reflection reverses handedness
        GL.invertCulling = true;

        try
        {
            reflectionCamera.Render();
        }
        finally
        {
            GL.invertCulling = false;
        }

        // Reflection projection matrix
        Matrix4x4 reflectionVP =
            GL.GetGPUProjectionMatrix(
                reflectionCamera.projectionMatrix,
                true
            ) * reflectionCamera.worldToCameraMatrix;

        waterRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetTexture(
            Shader.PropertyToID(reflectionTextureName),
            reflectionTexture
        );

        propertyBlock.SetMatrix(
            Shader.PropertyToID(reflectionMatrixName),
            reflectionVP
        );

        propertyBlock.SetFloat(
            Shader.PropertyToID(reflectionYFlipName),
            flipReflectionY ? 1f : 0f
        );

        waterRenderer.SetPropertyBlock(propertyBlock);
    }

    void CreateRenderTexture(int width, int height)
    {
        ReleaseRenderTexture();

        textureWidth = width;
        textureHeight = height;

        reflectionTexture = new RenderTexture(
            width,
            height,
            16,
            useHDR ? RenderTextureFormat.DefaultHDR :
                     RenderTextureFormat.Default
        );

        reflectionTexture.name = "Planar Reflection RT";
        reflectionTexture.filterMode = FilterMode.Bilinear;
        reflectionTexture.wrapMode = TextureWrapMode.Clamp;
        reflectionTexture.useMipMap = false;
        reflectionTexture.autoGenerateMips = false;

        reflectionTexture.Create();
    }

    Vector4 CameraSpacePlane(
        Camera cam,
        Vector3 position,
        Vector3 normal,
        float offset)
    {
        Vector3 offsetPosition = position + normal * offset;

        Matrix4x4 view = cam.worldToCameraMatrix;

        Vector3 cameraPosition = view.MultiplyPoint(offsetPosition);
        Vector3 cameraNormal = view.MultiplyVector(normal).normalized;

        return new Vector4(
            cameraNormal.x,
            cameraNormal.y,
            cameraNormal.z,
            -Vector3.Dot(cameraPosition, cameraNormal)
        );
    }

    Matrix4x4 CalculateReflectionMatrix(Vector4 p)
    {
        Matrix4x4 m = Matrix4x4.zero;

        m.m00 = 1f - 2f * p.x * p.x;
        m.m01 = -2f * p.x * p.y;
        m.m02 = -2f * p.x * p.z;
        m.m03 = -2f * p.x * p.w;

        m.m10 = -2f * p.y * p.x;
        m.m11 = 1f - 2f * p.y * p.y;
        m.m12 = -2f * p.y * p.z;
        m.m13 = -2f * p.y * p.w;

        m.m20 = -2f * p.z * p.x;
        m.m21 = -2f * p.z * p.y;
        m.m22 = 1f - 2f * p.z * p.z;
        m.m23 = -2f * p.z * p.w;

        m.m30 = 0f;
        m.m31 = 0f;
        m.m32 = 0f;
        m.m33 = 1f;

        return m;
    }

    void ReleaseRenderTexture()
    {
        if (reflectionTexture == null)
            return;

        reflectionTexture.Release();
        Destroy(reflectionTexture);
        reflectionTexture = null;
    }

    void OnDisable()
    {
        ReleaseRenderTexture();

        if (reflectionCamera != null)
            Destroy(reflectionCamera.gameObject);
    }
}