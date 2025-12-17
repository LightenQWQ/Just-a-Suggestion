using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;
using System.Text.RegularExpressions;

public class MangaGameController : MonoBehaviour
{
    [Header("=== UI 綁定 ===")]
    public RawImage comicDisplay;       
    public TextMeshProUGUI dialogueText;
    public TMP_InputField playerInput;  
    public Button sendButton;           

    [Header("=== 初始設定 ===")]
    public Texture2D startImage; 

    [Header("=== AI 設定 ===")]
    public string ollamaUrl = "http://localhost:11434/api/generate";
    public string ollamaModelName = "llama3"; 
    public string sdApiUrl = "http://127.0.0.1:7860/sdapi/v1/txt2img";

    [Header("=== 1. 畫風設定 (修復：清晰漫畫線條) ===")]
    [TextArea(2, 6)]
    // 【修改重點】拿掉了 heavy ink, gritty 等髒髒的詞，改用 precise lineart
    public string baseStylePrompt = "(manga style:1.5), (monochrome:1.3), (greyscale:1.3), (precise lineart:1.4), (traditional media:1.2), detailed background, comic panel, sharp focus, masterpiece, best quality, ";
    
    [Header("=== 2. 臉部設定 (鎖定：短髮少年) ===")]
    [TextArea(2, 5)]
    // 根據您的圖片：短黑髮、蒼白、瘦弱
    public string characterFacePrompt = "1boy, male teenager, messy short black hair, pale skin, skinny body, (consistent face:1.3), ";
    
    [Header("=== 3. 服裝設定 (鎖定：白T黑褲光腳) ===")]
    [TextArea(2, 5)]
    // 根據您的圖片：髒白T、黑短褲、光腳
    public string currentClothes = "wearing dirty white t-shirt, black shorts, barefoot, ";

    [Header("=== 負面提示 (乾淨版) ===")]
    [TextArea(2, 5)]
    // 拿掉一些可能會讓線條斷裂的詞
    public string negativePrompt = "(sepia:1.5), (brown:1.5), (yellow:1.5), (color:1.5), alternate costume, changing clothes, different outfit, 3d, realistic, blurry, bad anatomy, text, watermark, low quality, smooth skin, cartoon, anime, cute, kawaii, vector art, cell shading, sketch, abstract, messy";

    void Start()
    {
        dialogueText.text = "（你醒來時，發現自己躺在一個陌生的房間裡...）";
        
        sendButton.onClick.AddListener(OnSendClicked);
        playerInput.Select();
        playerInput.ActivateInputField();
        
        if (startImage != null)
        {
            comicDisplay.texture = startImage;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (sendButton.interactable && !string.IsNullOrEmpty(playerInput.text))
            {
                OnSendClicked();
            }
        }
    }

    void OnSendClicked()
    {
        if (string.IsNullOrEmpty(playerInput.text) || !sendButton.interactable) return;
        
        sendButton.interactable = false;
        string textToSend = playerInput.text;
        playerInput.text = ""; 
        playerInput.DeactivateInputField(); 

        StartCoroutine(ProcessGameTurn(textToSend));
    }

    IEnumerator ProcessGameTurn(string userInput)
    {
        dialogueText.text = "（思考中...）";
        yield return CallOllama(userInput);

        if (!dialogueText.text.Contains("THE_END"))
        {
            sendButton.interactable = true;
            playerInput.Select();
            playerInput.ActivateInputField();
        }
    }

    IEnumerator CallOllama(string input)
    {
        // System Prompt: 導演模式 + 禁止描寫外觀
        string systemPrompt = 
            $"【角色設定】：你是一個被困在恐怖房間的少年。全程用「繁體中文」與玩家對話。\n" +
            $"【玩家說】：{input}\n" +
            "【分鏡規則】：請判斷畫面類型，並在第一行加上對應標籤：\n" +
            "1. 強烈情緒/說話 -> [FACE] 動作關鍵字 (例如：[FACE] crying, scared)\n" +
            "2. 觀察環境 -> [SCENE] 環境關鍵字 (例如：[SCENE] dirty room, corner)\n" +
            "3. 角色動作 -> [ACTION] 動作關鍵字 (例如：[ACTION] checking vase)\n" +
            "\n" +
            "【絕對禁止】：\n" +
            "不要描寫衣服、頭髮顏色或外貌！因為這些已經固定了。\n" +
            "只要描寫「姿勢」或「表情」就好。\n" +
            "\n" +
            "【回答範例】：\n" +
            "[ACTION] reaching out hand, crouching\n" +
            "這地板好像有東西...\n";

        OllamaRequest req = new OllamaRequest
        {
            model = ollamaModelName,
            prompt = systemPrompt,
            stream = false
        };

        string json = JsonUtility.ToJson(req);
        UnityWebRequest request = new UnityWebRequest(ollamaUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            OllamaResponse response = JsonUtility.FromJson<OllamaResponse>(request.downloadHandler.text);
            string aiText = response.response;

            string imagePrompt = "";
            string dialogue = "";
            string mode = "ACTION"; 

            string[] lines = aiText.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine)) continue;

                if (Regex.IsMatch(cleanLine, @"[\u4e00-\u9fff]"))
                {
                    // 潔淨文字
                    string chinese = Regex.Replace(cleanLine, "[a-zA-Z]", "")
                                          .Replace(":", "")
                                          .Replace("*", "")
                                          .Replace("[", "")
                                          .Replace("]", "")
                                          .Trim();
                    dialogue += chinese;
                }
                else
                {
                    // 解析導演標籤
                    if (cleanLine.Contains("[FACE]")) 
                    { 
                        mode = "FACE"; 
                        imagePrompt = cleanLine.Replace("[FACE]", "").Trim(); 
                    }
                    else if (cleanLine.Contains("[SCENE]")) 
                    { 
                        mode = "SCENE"; 
                        imagePrompt = cleanLine.Replace("[SCENE]", "").Trim(); 
                    }
                    else if (cleanLine.Contains("[ACTION]")) 
                    { 
                        mode = "ACTION"; 
                        imagePrompt = cleanLine.Replace("[ACTION]", "").Trim(); 
                    }
                    else if (imagePrompt == "" && cleanLine.Length > 3) 
                    { 
                        imagePrompt = cleanLine; 
                    }
                }
            }

            if (string.IsNullOrEmpty(dialogue)) dialogue = "...";
            dialogueText.text = dialogue;
            
            bool isEnding = aiText.Contains("THE_END");
            if (isEnding) dialogueText.text += "\n<color=red>-- 結局 --</color>";

            yield return CallStableDiffusion(imagePrompt, mode);
        }
        else
        {
            dialogueText.text = "AI 連線錯誤: " + request.error;
            sendButton.interactable = true;
        }
    }

    IEnumerator CallStableDiffusion(string rawPrompt, string mode)
    {
        string finalPrompt = "";
        string characterFull = characterFacePrompt + currentClothes;

        if (mode == "SCENE")
        {
            finalPrompt = $"{baseStylePrompt} (no humans:1.5), (scenery:1.4), (empty room:1.3), indoor, {rawPrompt}";
        }
        else if (mode == "FACE")
        {
            finalPrompt = $"{baseStylePrompt} {characterFull} (close up face:1.4), (looking at camera:1.2), emotional, {rawPrompt}";
        }
        else // ACTION
        {
            finalPrompt = $"{baseStylePrompt} {characterFull} (upper body:1.2), action shot, {rawPrompt}";
        }

        finalPrompt += ", (monochrome:1.3), (greyscale:1.3)";
        
        Debug.Log($"[{mode}] 生成: {finalPrompt}");

        SDRequest sdReq = new SDRequest();
        sdReq.prompt = finalPrompt;
        sdReq.negative_prompt = negativePrompt;
        sdReq.width = 512; 
        sdReq.height = 768; 
        sdReq.steps = 25; 
        sdReq.cfg_scale = 7; 

        string json = JsonUtility.ToJson(sdReq);

        UnityWebRequest request = new UnityWebRequest(sdApiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                SDResponse response = JsonUtility.FromJson<SDResponse>(request.downloadHandler.text);
                if (response.images != null && response.images.Length > 0)
                {
                    byte[] imageBytes = Convert.FromBase64String(response.images[0]);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(imageBytes);
                    
                    comicDisplay.texture = tex;
                }
            }
            catch (Exception e) { Debug.LogError("圖片處理錯誤: " + e.Message); }
        }
    }

    [Serializable] public class OllamaRequest { public string model; public string prompt; public bool stream; }
    [Serializable] public class OllamaResponse { public string response; }
    [Serializable] public class SDRequest { public string prompt; public string negative_prompt; public int steps; public int width; public int height; public int cfg_scale; }
    [Serializable] public class SDResponse { public string[] images; }
}