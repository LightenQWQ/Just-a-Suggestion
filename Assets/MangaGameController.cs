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

    [Header("=== AI 設定 ===")]
    public string ollamaUrl = "http://localhost:11434/api/generate";
    public string ollamaModelName = "llama3"; 
    public string sdApiUrl = "http://127.0.0.1:7860/sdapi/v1/txt2img";

    [Header("=== 漫畫風格 (Marvin LoRA) ===")]
    [TextArea(2, 6)]
    public string baseStylePrompt = "<lora:marvin:0.8>, (manga style:1.4), (monochrome:1.3), (greyscale:1.2), (lineart:1.4), screentone, comic panel, high contrast, ink sketch, traditional media, masterpiece, best quality, ";
    
    [TextArea(2, 5)]
    public string characterPrompt = "1boy, male teenager, messy black hair, wearing a hoodie, hollow eyes, pale skin, ";
    
    [TextArea(2, 5)]
    public string negativePrompt = "color, 3d, realistic, cgi, photorealistic, blurry, bad anatomy, extra limbs, watermark, text, speech bubble, low quality, smooth skin, sketch, charcoal, smudge, soft focus, oil painting, watercolor, dirty, sepia";

    void Start()
    {
        dialogueText.text = "（你看到一個少年倒在黑暗的地上，氣氛令人窒息...）";
        sendButton.onClick.AddListener(OnSendClicked);
        playerInput.Select();
        playerInput.ActivateInputField();
        
        // 遊戲開始時，先幫玩家畫第一張圖！
        StartCoroutine(CallStableDiffusion("lying on floor, dark room, horror atmosphere"));
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
        if (string.IsNullOrEmpty(playerInput.text)) return;
        StartCoroutine(ProcessGameTurn(playerInput.text));
    }

    IEnumerator ProcessGameTurn(string userInput)
    {
        sendButton.interactable = false;
        string originalInput = userInput;
        
        playerInput.text = ""; 
        playerInput.DeactivateInputField(); 

        dialogueText.text = "（少年正在思考...）";

        yield return CallOllama(originalInput);

        if (!dialogueText.text.Contains("THE_END"))
        {
            sendButton.interactable = true;
            playerInput.Select();
            playerInput.ActivateInputField();
        }
    }

    IEnumerator CallOllama(string input)
    {
        string systemPrompt = 
            $"【指令】：你是一個互動漫畫遊戲的角色，必須全程使用「繁體中文」回答。\n" +
            $"你是受困在詭異房間的少年。玩家說：「{input}」。\n" +
            "【結局判斷邏輯】：\n" +
            "1. 戀愛 (LOVE): 扭曲笑容。\n" +
            "2. 發瘋 (MADNESS): 抓破臉皮。\n" +
            "3. 死亡 (DEATH): 肢體扭曲。\n" +
            "4. 放棄 (GIVE_UP): 融化癱軟。\n" +
            "5. 普通 (NORMAL): 繼續對話。\n" +
            "【回答格式】：\n" +
            "請嚴格遵守格式： [英文圖片關鍵字] || [中文台詞]\n" +
            "範例：looking around || 這裡是哪裡？\n";

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

            // 【重要修正】這裡加入了防呆機制
            string imagePrompt = "scary dark room, horror atmosphere, face closeup"; // 預設圖片關鍵字
            string dialogue = aiText;

            string[] parts = aiText.Split(new string[] { "||" }, StringSplitOptions.None);
            
            if (parts.Length >= 2)
            {
                // 如果 AI 乖乖遵守格式
                imagePrompt = parts[0].Replace("[", "").Replace("]", "").Trim();
                dialogue = parts[1].Trim();
            }
            else
            {
                // 如果 AI 不聽話，我們就直接用它的回答來畫圖，或者用預設詞
                Debug.LogWarning("AI 沒給圖片指令，使用預設值");
                // 這裡可以選擇把它的對話當成關鍵字，或是保持預設
            }

            // 清理對話中的標籤
            dialogue = Regex.Replace(dialogue, @"\[.*?\]", "").Trim(); 
            bool isEnding = aiText.Contains("THE_END"); 

            dialogueText.text = dialogue;
            if (isEnding) dialogueText.text += "\n<color=red>-- 結局 --</color>";

            // 【關鍵】無論如何，這裡一定會被執行！
            yield return CallStableDiffusion(imagePrompt);
        }
        else
        {
            dialogueText.text = "AI 連線錯誤: " + request.error;
        }
    }

    IEnumerator CallStableDiffusion(string actionPrompt)
    {
        string finalPrompt = $"{baseStylePrompt}{characterPrompt}{actionPrompt}";
        Debug.Log("SD Prompt: " + finalPrompt);

        SDRequest sdReq = new SDRequest();
        sdReq.prompt = finalPrompt;
        sdReq.negative_prompt = negativePrompt;
        sdReq.width = 512; 
        sdReq.height = 768; 
        sdReq.steps = 22; 
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
                    comicDisplay.SetNativeSize(); 
                }
            }
            catch (Exception e) { Debug.LogError("圖片錯誤: " + e.Message); }
        }
    }

    [Serializable] public class OllamaRequest { public string model; public string prompt; public bool stream; }
    [Serializable] public class OllamaResponse { public string response; }
    [Serializable] public class SDRequest { 
        public string prompt; 
        public string negative_prompt; 
        public int steps; 
        public int width; 
        public int height; 
        public int cfg_scale; 
    }
    [Serializable] public class SDResponse { public string[] images; }
}