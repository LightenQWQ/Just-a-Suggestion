using System;
using System.Collections.Generic;

// ReSharper disable InconsistentNaming

namespace AiToolbox {
// https://platform.openai.com/docs/guides/images

[Serializable]
public struct DalleRequest {
    public string model; // "dall-e-3"
    public string prompt; // "a painting of a sunset over the ocean"
    public string size; // 1024x1024, 1024x1792 or 1792x1024
    public string quality; // "standard"
    public int n; // 1
}

[Serializable]
public class UrlClass {
    public string url;
}

[Serializable]
public class CreateImageResponse {
    public int created;
    public List<UrlClass> data;
}
}