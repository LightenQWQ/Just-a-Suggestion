using System;
using UnityEngine;

namespace AiToolbox {
/// <summary>
/// Settings for the AI Toolbox calls to DALL-E image generation API.
/// </summary>
[Serializable]
public class DalleParameters : ISerializationCallbackReceiver {
    // ReSharper disable InconsistentNaming
    public enum Model {
        [InspectorName("dall-e-3")]
        Dalle3,
    }

    // 1024x1024, 1024x1792 or 1792x1024
    public enum Size {
        [InspectorName("1024x1024")]
        Size1024x1024,
        [InspectorName("1024x1792")]
        Size1024x1792,
        [InspectorName("1792x1024")]
        Size1792x1024,
    }

    public enum Quality {
        [InspectorName("standard")]
        Standard,
        [InspectorName("hd")]
        Hd,
    }
    // ReSharper restore InconsistentNaming

    public string apiKey;
    public ApiKeyEncryption apiKeyEncryption;
    public string apiKeyRemoteConfigKey;
    public string apiKeyEncryptionPassword;

    [Tooltip("The version of DALL-E model to use for image generation.")]
    public Model model = Model.Dalle3;

    [Tooltip("The size of the generated image. DALL-E supports only these predefined sizes.")]
    public Size size = Size.Size1024x1024;

    [Tooltip("The quality of the generated image. Standard quality images are the fastest to generate.")]
    public Quality quality = Quality.Standard;

    public int timeout;
    public int throttle;

    [SerializeField, HideInInspector]
    private bool _serialized;

    public DalleParameters(string apiKey) {
        this.apiKey = apiKey;
    }

    public DalleParameters(DalleParameters parameters) {
        apiKey = parameters.apiKey;
        apiKeyEncryption = parameters.apiKeyEncryption;
        apiKeyRemoteConfigKey = parameters.apiKeyRemoteConfigKey;
        apiKeyEncryptionPassword = parameters.apiKeyEncryptionPassword;
        timeout = parameters.timeout;
        throttle = parameters.throttle;
        _serialized = parameters._serialized;
    }

    public void OnBeforeSerialize() {
        if (_serialized) return;
        _serialized = true;
        timeout = 0;
        throttle = 0;
        apiKeyRemoteConfigKey = "openai_api_key";
        apiKeyEncryptionPassword = Guid.NewGuid().ToString();
    }

    public void OnAfterDeserialize() { }

    internal string GetModelString() {
        return model switch {
            Model.Dalle3 => "dall-e-3",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    internal string GetSizeString() {
        return size switch {
            Size.Size1024x1024 => "1024x1024",
            Size.Size1024x1792 => "1024x1792",
            Size.Size1792x1024 => "1792x1024",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    internal string GetQualityString() {
        return quality switch {
            Quality.Standard => "standard",
            Quality.Hd => "hd",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
}