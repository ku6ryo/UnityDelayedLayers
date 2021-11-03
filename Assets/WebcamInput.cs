using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class WebcamInput : MonoBehaviour
{
    struct ThreadSize
    {
      public uint x;
      public uint y;
      public uint z;

      public ThreadSize(uint x, uint y, uint z)
      {
        this.x = x;
        this.y = y;
        this.z = z;
      }
    }

    [SerializeField] Vector2Int _resolution = new Vector2Int(1920, 1080);
    [SerializeField] RawImage processedImage;
    [SerializeField] ComputeShader blendShader;

    WebCamTexture _webcamTexture;
    List<RenderTexture> renderTextures = new List<RenderTexture>();
    RenderTexture _tmpRenderTexture1;
    RenderTexture _tmpRenderTexture2;

    int numTextures = 8;

    float lastCapturedTime = 0f;

    float gapTime = 0.1f;

    void Start()
    {
        _webcamTexture = new WebCamTexture("", _resolution.x, _resolution.y);
        _tmpRenderTexture1 = new RenderTexture(_resolution.x, _resolution.y, 0, RenderTextureFormat.ARGBFloat);
        _tmpRenderTexture1.enableRandomWrite = true;
        _tmpRenderTexture1.Create();
        _tmpRenderTexture2 = new RenderTexture(_resolution.x, _resolution.y, 0, RenderTextureFormat.ARGBFloat);
        _tmpRenderTexture2.enableRandomWrite = true;
        _tmpRenderTexture2.Create();
        _webcamTexture.Play();
    }

    void OnDestroy()
    {
        if (_webcamTexture != null) Destroy(_webcamTexture);
        if (_tmpRenderTexture1 != null) Destroy(_tmpRenderTexture1);
        if (_tmpRenderTexture2 != null) Destroy(_tmpRenderTexture2);
        for (int i = 0; i < renderTextures.Count; i++)
        {
          var tex = renderTextures[i];
          renderTextures.Remove(tex);
          Destroy(tex);
        }
    }

    void Update()
    {
        if (!_webcamTexture.didUpdateThisFrame) return;

        var kernelIndex = blendShader.FindKernel("CSMain");
        ThreadSize threadSize = new ThreadSize();
        blendShader.GetKernelThreadGroupSizes(kernelIndex, out threadSize.x, out threadSize.y, out threadSize.z);

        if (Time.time - lastCapturedTime > gapTime) {
          if (renderTextures.Count == numTextures) {
            var oldest = renderTextures[0];
            renderTextures.Remove(oldest);
            Destroy(oldest);
          }
          var latest = new RenderTexture(_resolution.x, _resolution.y, 0, RenderTextureFormat.ARGBFloat);
          latest.enableRandomWrite = true;
          latest.Create();
          Graphics.Blit(_webcamTexture, latest);
          renderTextures.Add(latest);
          lastCapturedTime = Time.time;
        }
        var source = _tmpRenderTexture2;
        var result = _tmpRenderTexture1;
        RenderTexture.active = _tmpRenderTexture2;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = null;
        for (int i = 0; i < renderTextures.Count; i++) {
          if (i % 2 == 0)
          {
            source = _tmpRenderTexture2;
            result = _tmpRenderTexture1;
          }
          else
          {
            source = _tmpRenderTexture1;
            result = _tmpRenderTexture2;
          }
          blendShader.SetTexture(kernelIndex, "Texture1", source);
          blendShader.SetTexture(kernelIndex, "Texture2", renderTextures[i]);
          blendShader.SetTexture(kernelIndex, "Result", result);
          blendShader.SetFloat("ratio", 1.0f / numTextures);
          blendShader.Dispatch(
            kernelIndex,
            _resolution.x / (int) threadSize.x,
            _resolution.y / (int) threadSize.y,
            (int) threadSize.z
          );
        }

        blendShader.GetKernelThreadGroupSizes(kernelIndex, out threadSize.x, out threadSize.y, out threadSize.z);
        blendShader.SetTexture(kernelIndex, "Texture1", result);
        blendShader.SetTexture(kernelIndex, "Texture2", _webcamTexture);
        blendShader.SetTexture(kernelIndex, "Result", source);
        blendShader.SetFloat("ratio", 1.0f / (numTextures + 1));
        blendShader.Dispatch(
          kernelIndex,
          _resolution.x / (int) threadSize.x,
          _resolution.y / (int) threadSize.y,
          (int) threadSize.z
        );
        processedImage.texture = source;
    }
}
