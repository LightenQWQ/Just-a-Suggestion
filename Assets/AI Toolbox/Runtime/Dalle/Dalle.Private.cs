using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace AiToolbox {
public static partial class Dalle {
    private static readonly List<RequestRecord> RequestRecords = new();

    private static Action RequestBlocking(string prompt, DalleParameters parameters, Action<Texture2D> completeCallback,
                                          Action<long, string> failureCallback) {
        Debug.Assert(parameters != null, "Parameters cannot be null.");
        Debug.Assert(!string.IsNullOrEmpty(parameters!.apiKey), "API key cannot be null or empty.");
        Debug.Assert(!string.IsNullOrEmpty(prompt), "Input text cannot be null or empty.");

        if (parameters.throttle > 0) {
            var requestCount = RequestRecords.Count;
            if (requestCount >= parameters.throttle) {
                failureCallback?.Invoke((long)ErrorCodes.ThrottleExceeded,
                                        $"Too many requests. Maximum allowed: {parameters.throttle}.");
                return () => { };
            }
        }

        var requestRecord = new RequestRecord();
        var webRequest = GetWebRequest(prompt, parameters, failureCallback, requestRecord);
        var cancelCallback = new Action(() => {
            try {
                webRequest?.Abort();
                webRequest?.Dispose();
                RequestRecords.Remove(requestRecord);
            }
            catch (Exception) {
                // If the request is aborted, accessing the error property will throw an exception.
            }
        });
        requestRecord.SetCancelCallback(cancelCallback);
        RequestRecords.Add(requestRecord);

        webRequest.SendWebRequest().completed += _ => {
            RequestRecords.Remove(requestRecord);
            Application.quitting -= cancelCallback;

            bool isErrorResponse;
            try {
                isErrorResponse = !string.IsNullOrEmpty(webRequest.error);
            }
            catch (Exception) {
                // If the request is aborted, accessing the error property will throw an exception.
                return;
            }

            if (isErrorResponse) {
                var message = $"{webRequest.error}\n" + // 
                              $"{webRequest.downloadHandler.error}\n" + //
                              $"{webRequest.downloadHandler.text}";
                Debug.LogError(message);
                failureCallback?.Invoke(webRequest.responseCode, message);
                return;
            }

            var response = JsonUtility.FromJson<CreateImageResponse>(webRequest.downloadHandler.text);
            var downloadImageWebRequest = UnityWebRequestTexture.GetTexture(response.data[0].url);
            var downloadImageOperation = downloadImageWebRequest.SendWebRequest();

            downloadImageOperation.completed += _ => {
                if (downloadImageWebRequest.result != UnityWebRequest.Result.Success) {
                    failureCallback?.Invoke(downloadImageWebRequest.responseCode, downloadImageWebRequest.error);
                    return;
                }

                var texture = DownloadHandlerTexture.GetContent(downloadImageWebRequest);
                if (texture == null) {
                    var message = "Failed to create texture from downloaded bytes. Response: " +
                                  $"{webRequest.downloadHandler.text}";
                    failureCallback?.Invoke(downloadImageWebRequest.responseCode, message);
                    return;
                }

                texture.name = "Generated Image";
                completeCallback?.Invoke(texture);
                webRequest.Dispose();
            };
        };

        Application.quitting += cancelCallback;
        return cancelCallback;
    }

    private static IEnumerator GetRemoteConfig(DalleParameters parameters, Action<long, string> failureCallback) {
        var apiKeySet = false;
        var task = RemoteKeyService.GetApiKey(parameters.apiKeyRemoteConfigKey, s => {
            parameters.apiKeyEncryption = ApiKeyEncryption.None;
            parameters.apiKey = s;
            apiKeySet = true;
        }, (errorCode, error) => {
            failureCallback?.Invoke(errorCode, error);
            apiKeySet = true;
        });

        yield return new WaitUntil(() => task.IsCompleted && apiKeySet);

        if (task.IsFaulted) {
            failureCallback?.Invoke((long)ErrorCodes.RemoteConfigConnectionFailure,
                                    "Failed to retrieve API key from remote config.");
        }
    }

    private static UnityWebRequest GetWebRequest(string prompt, DalleParameters parameters,
                                                 Action<long, string> failureCallback, RequestRecord requestRecord) {
        const string url = "https://api.openai.com/v1/images/generations";
        var requestObject = new DalleRequest {
            prompt = prompt,
            model = parameters.GetModelString(),
            size = parameters.GetSizeString(),
            quality = parameters.GetQualityString(),
            n = 1,
        };
        var json = JsonUtility.ToJson(requestObject);
#if UNITY_2022_2_OR_NEWER
        var request = UnityWebRequest.Post(url, json, "application/json");
#else
        var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
#endif
        request.timeout = parameters.timeout;

        try {
            var apiKey = parameters.apiKey;
            var isEncrypted = parameters.apiKeyEncryption == ApiKeyEncryption.LocallyEncrypted;
            if (isEncrypted) {
                apiKey = Key.B(apiKey, parameters.apiKeyEncryptionPassword);
            }

            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        }
        catch (Exception e) {
            failureCallback?.Invoke((long)ErrorCodes.Unknown, e.Message);
            RequestRecords.Remove(requestRecord);
        }

        return request;
    }
}
}