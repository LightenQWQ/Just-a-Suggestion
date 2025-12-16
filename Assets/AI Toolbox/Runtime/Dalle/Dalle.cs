using System;
using System.Collections;
using UnityEngine;

namespace AiToolbox {
public static partial class Dalle {
    /// <summary>
    /// Request image generation from the prompt text.
    /// </summary>
    /// <param name="prompt">Textual description of the image to generate, e.g. "a painting of a sunset".</param>
    /// <param name="parameters">The parameters for the image generation request.</param>
    /// <param name="completeCallback">The callback to be called when the request is completed.</param>
    /// <param name="failureCallback">The callback to be called when the request fails.</param>
    /// <returns>A function that can be called to cancel the request.</returns>
    public static Action Request(string prompt, DalleParameters parameters, Action<Texture2D> completeCallback,
                                 Action<long, string> failureCallback) {
        if (parameters.apiKeyEncryption != ApiKeyEncryption.RemoteConfig) {
            return RequestBlocking(prompt, parameters, completeCallback, failureCallback);
        }

        var enumerator = RequestCoroutine(prompt, parameters, completeCallback, failureCallback);
        DalleContainer.instance.StartCoroutine(enumerator);

        return CancelCallback;

        static IEnumerator RequestCoroutine(string prompt, DalleParameters parameters,
                                            Action<Texture2D> completeCallback, Action<long, string> failureCallback) {
            if (parameters.apiKeyEncryption == ApiKeyEncryption.RemoteConfig) {
                yield return GetRemoteConfig(parameters, failureCallback);
            }

            RequestBlocking(prompt, parameters, completeCallback, failureCallback);
        }

        void CancelCallback() {
            DalleContainer.instance.StopCoroutine(enumerator);
        }
    }

    /// <summary>
    /// Cancel all pending text-to-speech requests.
    /// </summary>
    public static void CancelAllRequests() {
        while (RequestRecords.Count > 0) {
            RequestRecords[0].Cancel();
        }

        RequestRecords.Clear();
    }
}
}