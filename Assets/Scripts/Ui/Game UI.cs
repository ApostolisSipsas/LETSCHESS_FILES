using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public enum CameraAngle
{
    menu = 0,
    whiteTeam = 1,
    blackTeam = 2
}

public class GameUI : MonoBehaviour
{
    [SerializeField] private Animator MenuAnimator;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private GameObject[] cameraAngles;
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject HostMenu; // = OnlineMenu στη σκηνή

    public static GameUI Instance { set; get; }
    public Action<bool> SetLocalGame;

    public Server server;
    public Client client;

    private TMP_FontAsset gameFont;

    // Panels που δημιουργούνται κατά την εκτέλεση
    private GameObject nameInputPanel;
    private TMP_InputField hostNameInput;
    private TMP_InputField hostPinInput;
    private GameObject waitingPanel;
    private TMP_Text waitingText;

    // Dialog εισαγωγής PIN για προστατευμένα lobbies
    private GameObject pinDialogPanel;
    private TMP_InputField pinDialogInput;
    private TMP_Text pinDialogError;

    // Κατάσταση επιλογής lobby
    private string selectedIp = null;
    private ushort selectedPort = 0;
    private string selectedPin = "";
    private GameObject selectedRow = null;
    private Button lobbyConnectButton = null;
    private TMP_Text connectionErrorText = null;
    private Coroutine connectionTimeoutCoroutine = null;
    private bool gameStarted = false;
    private static readonly Color rowNormal   = new Color(0.40f, 0.31f, 0.31f, 1f);
    private static readonly Color rowSelected = new Color(0.65f, 0.35f, 0.35f, 1f);

    // Συντομεύσεις για τα κύρια panels του online μενού
    private GameObject hostOrConnectPanel => HostMenu.transform.GetChild(0).gameObject;
    private GameObject hostListPanel      => HostMenu.transform.GetChild(1).gameObject;

    // -------------------------------------------------------
    void Awake()
    {
        Instance = this;
        RegisterEvents();
    }

    void Start()
    {
        TMP_Text anyText = FindFirstObjectByType<TMP_Text>();
        if (anyText != null) gameFont = anyText.font;

        BuildNameInputPanel();
        BuildWaitingPanel();
        SetupScrollContent();
        BuildLobbyConnectButton();
        BuildConnectionErrorOverlay();
        BuildPinDialog();
    }
    // ΚΑΤΑΣΚΕΥΗ PANELS 
    private void BuildNameInputPanel()
    {
        // Full-screen child του Canvas 
        Transform canvas = HostMenu.transform.parent;

        nameInputPanel = MakePanel("NameInputPanel", canvas,
            new Color(0.40f, 0.31f, 0.31f, 1f));
        nameInputPanel.SetActive(false);

        VerticalLayoutGroup vlg = nameInputPanel.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 30;
        vlg.padding = new RectOffset(100, 100, 120, 80);
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        CreateTMPLabel(nameInputPanel.transform, "Enter your name", 70, Color.white, new Vector2(700, 100));
        hostNameInput = CreateInputField(nameInputPanel.transform, "Your name...", new Vector2(700, 130));
        CreateTMPLabel(nameInputPanel.transform, "PIN (optional, 1-4 digits)", 45,
            new Color(0.8f, 0.7f, 0.7f, 1f), new Vector2(700, 70));
        hostPinInput = CreateInputField(nameInputPanel.transform, "4 digit code", new Vector2(700, 110));
        hostPinInput.characterLimit = 4;
        hostPinInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        CreateButton(nameInputPanel.transform, "HOST", new Vector2(500, 150),
            new Color(0.79f, 0.38f, 0.38f, 1f), OnConfirmHostButtonClick);

        CreateButton(nameInputPanel.transform, "BACK", new Vector2(300, 100),
            new Color(0.55f, 0.25f, 0.25f, 1f), OnNameInputBackButtonClick);
    }

    private void BuildWaitingPanel()
    {
        Transform canvas = HostMenu.transform.parent;

        waitingPanel = MakePanel("WaitingPanel", canvas,
            new Color(0.40f, 0.31f, 0.31f, 1f));
        waitingPanel.SetActive(false);

        VerticalLayoutGroup vlg = waitingPanel.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 80;
        vlg.padding = new RectOffset(100, 100, 300, 100);
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        // Μήνυμα αναμονής σύνδεσης αντιπάλου
        waitingText = CreateTMPLabel(waitingPanel.transform, "", 65, Color.white, new Vector2(900, 300));
        waitingText.alignment = TextAlignmentOptions.Center;

        // Κουμπί ακύρωσης αναμονής
        CreateButton(waitingPanel.transform, "BACK", new Vector2(300, 100),
            new Color(0.55f, 0.25f, 0.25f, 1f), OnWaitingBackButtonClick);
    }

    private void SetupScrollContent()
    {
        ScrollRect sr = hostListPanel.GetComponentInChildren<ScrollRect>(true);
        if (sr == null || sr.content == null) return;

        Transform content = sr.content;
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, contentRT.sizeDelta.y);

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 12;
        vlg.padding              = new RectOffset(12, 12, 12, 12);
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;   
        vlg.childAlignment         = TextAnchor.UpperCenter;
    }

  
    // ΔΙΑΧΕΙΡΙΣΗ ΚΑΜΕΡΑΣ


    public void ChangeCamera(CameraAngle idx)
    {
        for (int i = 0; i < cameraAngles.Length; i++)
            cameraAngles[i].SetActive(false);
        cameraAngles[(int)idx].SetActive(true);
    }

    public Camera GetCurrentCamera()
    {
        foreach (var go in cameraAngles)
            if (go != null && go.activeSelf)
            {
                Camera cam = go.GetComponentInChildren<Camera>();
                if (cam != null) return cam;
            }
        return Camera.main;
    }

    // ΧΕΙΡΙΣΤΕΣ ΚΟΥΜΠΙΩΝ

    public void OnLocalGameButtonClick()
    {
        MenuAnimator.SetTrigger("inGameMenu");
        server.ShutDown();
        client.ShutDown();
        ushort port = server.Init(8007);
        if (port == 0) return;
        client.Init("127.0.0.1", port);
        SetLocalGame?.Invoke(true);
        HideMenuPanels();
        ChangeCamera(CameraAngle.whiteTeam);
    }

    public void OnOnlineGameButtonClick()
    {
        SetLocalGame?.Invoke(false);
        MenuAnimator.SetTrigger("onlineMenu");
    }

    public void OnOnlineHostButtonClick()
    {
        hostOrConnectPanel.SetActive(false);
        nameInputPanel.SetActive(true);
    }

    private void OnConfirmHostButtonClick()
    {
        string hostName = (hostNameInput != null && hostNameInput.text.Trim().Length > 0)
            ? hostNameInput.text.Trim()
            : "Host";

        // μόνο ψηφία, μέγιστο 4
        string pin = "";
        if (hostPinInput != null && hostPinInput.text.Trim().Length > 0)
        {
            pin = System.Text.RegularExpressions.Regex.Replace(hostPinInput.text.Trim(), @"[^\d]", "");
            if (pin.Length > 4) pin = pin.Substring(0, 4);
        }

        SetLocalGame?.Invoke(false); 
        server.ShutDown();
        client.ShutDown();
        ushort port = server.Init(8007, hostName, pin);
        if (port == 0) { nameInputPanel.SetActive(false); hostOrConnectPanel.SetActive(true); return; }
        client.Init("127.0.0.1", port);

        waitingText.text = $"Hey {hostName}!\nWaiting for someone to connect...";
        nameInputPanel.SetActive(false);
        waitingPanel.SetActive(true);
    }

    // Επιστροφή από εισαγωγή ονόματος
    private void OnNameInputBackButtonClick()
    {
        nameInputPanel.SetActive(false);
        hostOrConnectPanel.SetActive(true);
    }

    // Επιστροφή από αναμονή 
    private void OnWaitingBackButtonClick()
    {
        server.ShutDown();
        client.ShutDown();
        SetLocalGame?.Invoke(false); 
        waitingPanel.SetActive(false);
        hostOrConnectPanel.SetActive(true);
    }

    private void ShowLobbyConnect(bool show)
    {
        if (lobbyConnectButton != null) lobbyConnectButton.gameObject.SetActive(show);
    }
    public void OnOnlineConnectButtonClick()
    {
        selectedIp = null;
        selectedPort = 0;
        selectedRow = null;
        if (lobbyConnectButton != null) lobbyConnectButton.interactable = false;
        hostOrConnectPanel.SetActive(false);
        hostListPanel.SetActive(true);
        ShowLobbyConnect(true);
        LobbyDiscovery.StartListening();
        PopulateLobby();
    }

    public void OnHostPickMenuBackButtonClick()
    {
        LobbyDiscovery.StopListening();
        ShowLobbyConnect(false);
        hostListPanel.SetActive(false);
        hostOrConnectPanel.SetActive(true);
    }

    public void OnOnlineBackButtonClick()
    {
        hostOrConnectPanel.SetActive(true);
        hostListPanel.SetActive(false);
        nameInputPanel.SetActive(false);
        waitingPanel.SetActive(false);
        ShowLobbyConnect(false);
        LobbyDiscovery.StopListening();
        MenuAnimator.SetTrigger("startMenu");
    }

    public void OnHostBackButtonClick()
    {
        server.ShutDown();
        client.ShutDown();
        MenuAnimator.SetTrigger("onlineMenu");
    }

    public void ConnectToHost(string ip, ushort port)
    {
        if (!LobbyDiscovery.HasFreshHost(ip))
        {
            ShowConnectionError("Room is no longer available.");
            return;
        }
        SetLocalGame?.Invoke(false);
        LobbyDiscovery.StopListening();
        ShowLobbyConnect(false);
        gameStarted = false;
        string connectIP = (ip == LobbyDiscovery.GetLocalIP()) ? "127.0.0.1" : ip;
        client.Init(connectIP, port);
        if (connectionTimeoutCoroutine != null) StopCoroutine(connectionTimeoutCoroutine);
        connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeout());
    }

    private System.Collections.IEnumerator ConnectionTimeout()
    {
        yield return new WaitForSeconds(5f);
        if (!gameStarted)
        {
            client.ShutDown();
            SetLocalGame?.Invoke(false);
            ShowConnectionError("Room is no longer available.");
        }
        connectionTimeoutCoroutine = null;
    }

    public void OnLeaveFromGameMenu()
    {
        ShowMenuPanels();
        hostOrConnectPanel.SetActive(true);
        hostListPanel.SetActive(false);
        nameInputPanel.SetActive(false);
        waitingPanel.SetActive(false);
        ShowLobbyConnect(false);
        LobbyDiscovery.StopListening();
        ChangeCamera(CameraAngle.menu);
        MenuAnimator.SetTrigger("StartMenu");
    }

    private void BuildConnectionErrorOverlay()
    {
        GameObject root = new GameObject("ConnectionErrorOverlay");
        Canvas c = root.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 20;
        root.AddComponent<UnityEngine.UI.CanvasScaler>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        RectTransform prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.05f, 0.38f);
        prt.anchorMax = new Vector2(0.95f, 0.58f);
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        UnityEngine.UI.Image img = panel.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.12f, 0.05f, 0.05f, 0.95f);

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(panel.transform, false);
        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20, 20);
        trt.offsetMax = new Vector2(-20, -20);
        connectionErrorText = txtGO.AddComponent<TextMeshProUGUI>();
        connectionErrorText.fontSize = 55;
        connectionErrorText.color = new Color(1f, 0.45f, 0.45f);
        connectionErrorText.alignment = TextAlignmentOptions.Center;
        connectionErrorText.enableWordWrapping = true;
        if (gameFont != null) connectionErrorText.font = gameFont;

        root.SetActive(false);
    }

    public void ShowConnectionError(string message)
    {
        // Επαναφορά UI lobby 
        ShowLobbyConnect(true);
        if (lobbyConnectButton != null) lobbyConnectButton.interactable = selectedIp != null;

        if (connectionErrorText == null) return;
        connectionErrorText.text = message;
        connectionErrorText.transform.parent.parent.gameObject.SetActive(true);
        StopCoroutine("HideConnectionError");
        StartCoroutine("HideConnectionError");
    }

    private System.Collections.IEnumerator HideConnectionError()
    {
        yield return new WaitForSeconds(4f);
        if (connectionErrorText != null)
            connectionErrorText.transform.parent.parent.gameObject.SetActive(false);
    }
    private void HideMenuPanels()
    {
        SetCanvasGroupVisible(menu, false);
        SetCanvasGroupVisible(HostMenu, false);
    }

    private void ShowMenuPanels()
    {
        SetCanvasGroupVisible(menu, true);
        SetCanvasGroupVisible(HostMenu, true);
    }

    private static void SetCanvasGroupVisible(GameObject go, bool visible)
    {
        if (go == null) return;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha          = visible ? 1f : 0f;
        cg.interactable   = visible;
        cg.blocksRaycasts = visible;
    }

    // ΔΙΑΧΕΙΡΙΣΗ LOBBY

    private void PopulateLobby()
    {
        ScrollRect sr = hostListPanel.GetComponentInChildren<ScrollRect>(true);
        if (sr == null || sr.content == null) return;
        Transform content = sr.content;

        foreach (Transform child in content)
            Destroy(child.gameObject);

        List<HostEntry> hosts = LobbyManager.GetHosts();

        // Διατήρηση επιλογής μόνο αν ο host εξακολουθεί να είναι στη λίστα
        bool keepSelection = false;
        if (selectedIp != null)
            foreach (var h in hosts)
                if (h.ip == selectedIp) { keepSelection = true; break; }

        if (!keepSelection)
        {
            selectedRow  = null;
            selectedIp   = null;
            selectedPort = 0;
            if (lobbyConnectButton != null) lobbyConnectButton.interactable = false;
        }

        if (hosts.Count == 0)
        {
            CreateLobbyLabel(content, "No hosts found...", new Color(0.7f, 0.5f, 0.5f, 1f));
            return;
        }

        foreach (HostEntry entry in hosts)
        {
            CreateLobbyEntry(content, entry.name, entry.ip, entry.port, entry.pin ?? "");

            // Επαναφορά highlight στη προηγουμένως επιλεγμένη σειρά
            if (keepSelection && entry.ip == selectedIp)
            {
                Transform row = content.GetChild(content.childCount - 1);
                Image bg = row.GetComponent<Image>();
                if (bg != null) bg.color = rowSelected;
                selectedRow = row.gameObject;
            }
        }
    }

    private void CreateLobbyEntry(Transform parent, string hostName, string ip, ushort port, string pin)
    {
        GameObject row = new GameObject("Entry_" + hostName);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 140);

        Image bg = row.AddComponent<Image>();
        bg.color = rowNormal;

        Button rowBtn = row.AddComponent<Button>();
        rowBtn.targetGraphic = bg;
        ColorBlock cb = rowBtn.colors;
        cb.normalColor      = rowNormal;
        cb.highlightedColor = new Color(0.55f, 0.40f, 0.40f, 1f);
        cb.pressedColor     = rowSelected;
        cb.selectedColor    = rowSelected;
        cb.fadeDuration     = 0.05f;
        rowBtn.colors = cb;
        rowBtn.transition = Selectable.Transition.ColorTint;

        string entryIp = ip;
        ushort entryPort = port;
        string entryPin = pin;
        rowBtn.onClick.AddListener(() => SelectHost(entryIp, entryPort, entryPin, row, bg));
        GameObject nameGO = new GameObject("HostName");
        nameGO.transform.SetParent(row.transform, false);
        RectTransform nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = Vector2.zero;
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(30, 0);
        nameRT.offsetMax = new Vector2(-30, 0);

        string pinTag = !string.IsNullOrEmpty(pin)
            ? "  <color=#ffcc44><size=40>[PIN]</size></color>"
            : "";
        TMP_Text txt = nameGO.AddComponent<TextMeshProUGUI>();
        txt.text = $"{hostName}{pinTag}  <size=38><color=#ffaaaa>{ip}:{port}</color></size>";
        txt.fontSize = 62;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.enableWordWrapping = false;
        txt.overflowMode = TextOverflowModes.Ellipsis;
        if (gameFont != null) txt.font = gameFont;
    }

    private void SelectHost(string ip, ushort port, string pin, GameObject row, Image rowBg)
    {
        if (selectedRow != null)
        {
            Image prev = selectedRow.GetComponent<Image>();
            if (prev != null) prev.color = rowNormal;
        }
        selectedIp   = ip;
        selectedPort = port;
        selectedPin  = pin;
        selectedRow  = row;
        rowBg.color  = rowSelected;
        if (lobbyConnectButton != null) lobbyConnectButton.interactable = true;
    }

    private void BuildLobbyConnectButton()
    {
        Transform canvas = HostMenu.transform.parent;

        GameObject go = new GameObject("Btn_CONNECT");
        go.transform.SetParent(canvas, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.04f);
        rt.anchorMax = new Vector2(0.92f, 0.14f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.SetActive(false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.79f, 0.38f, 0.38f, 1f);

        lobbyConnectButton = go.AddComponent<Button>();
        lobbyConnectButton.targetGraphic = img;
        ColorBlock cb = lobbyConnectButton.colors;
        cb.normalColor      = new Color(0.79f, 0.38f, 0.38f, 1f);
        cb.highlightedColor = new Color(0.96f, 0.56f, 0.56f, 1f);
        cb.pressedColor     = new Color(0.60f, 0.12f, 0.12f, 1f);
        cb.disabledColor    = new Color(0.35f, 0.22f, 0.22f, 0.6f);
        lobbyConnectButton.colors = cb;
        lobbyConnectButton.interactable = false;
        lobbyConnectButton.onClick.AddListener(() =>
        {
            if (selectedIp == null) return;
            if (!string.IsNullOrEmpty(selectedPin))
                ShowPinDialog();
            else
                ConnectToHost(selectedIp, selectedPort);
        });


        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        StretchFull(txtGO.AddComponent<RectTransform>());
        TMP_Text t = txtGO.AddComponent<TextMeshProUGUI>();
        t.text = "CONNECT";
        t.fontSize = 65;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        if (gameFont != null) t.font = gameFont;
    }

    private void BuildPinDialog()
    {
        Transform canvas = HostMenu.transform.parent;

        pinDialogPanel = MakePanel("PinDialog", canvas, new Color(0f, 0f, 0f, 0.75f));
        pinDialogPanel.SetActive(false);

        // Κεντρικό card του dialog PIN
        GameObject card = new GameObject("Card");
        card.transform.SetParent(pinDialogPanel.transform, false);
        RectTransform cardRT = card.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.15f, 0.3f);
        cardRT.anchorMax = new Vector2(0.85f, 0.7f);
        cardRT.offsetMin = Vector2.zero;
        cardRT.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = new Color(0.25f, 0.18f, 0.18f, 1f);

        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 25;
        vlg.padding = new RectOffset(60, 60, 50, 50);
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        CreateTMPLabel(card.transform, "Enter PIN:", 65, Color.white, new Vector2(600, 90));

        pinDialogInput = CreateInputField(card.transform, "PIN...", new Vector2(400, 110));
        pinDialogInput.characterLimit = 4;
        pinDialogInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        pinDialogInput.onValueChanged.AddListener(_ => { if (pinDialogError != null) pinDialogError.text = ""; });

        // Label σφάλματος PIN 
        GameObject errGO = new GameObject("ErrorText");
        errGO.transform.SetParent(card.transform, false);
        errGO.AddComponent<RectTransform>().sizeDelta = new Vector2(600, 60);
        pinDialogError = errGO.AddComponent<TextMeshProUGUI>();
        pinDialogError.text = "";
        pinDialogError.fontSize = 42;
        pinDialogError.color = new Color(1f, 0.45f, 0.45f, 1f);
        pinDialogError.alignment = TextAlignmentOptions.Center;
        if (gameFont != null) pinDialogError.font = gameFont;

        GameObject btnRow = new GameObject("BtnRow");
        btnRow.transform.SetParent(card.transform, false);
        btnRow.AddComponent<RectTransform>().sizeDelta = new Vector2(620, 110);
        HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 30;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        CreateButton(btnRow.transform, "CONNECT", new Vector2(280, 110),
            new Color(0.79f, 0.38f, 0.38f, 1f), OnPinDialogConfirm);
        CreateButton(btnRow.transform, "CANCEL", new Vector2(200, 110),
            new Color(0.55f, 0.25f, 0.25f, 1f), () => pinDialogPanel.SetActive(false));
    }

    private void ShowPinDialog()
    {
        if (pinDialogInput != null) pinDialogInput.text = "";
        if (pinDialogError != null) pinDialogError.text = "";
        pinDialogPanel.SetActive(true);
        pinDialogPanel.transform.SetAsLastSibling();
    }

    private void OnPinDialogConfirm()
    {
        string entered = System.Text.RegularExpressions.Regex.Replace(
            pinDialogInput != null ? pinDialogInput.text : "", @"[^\d]", "");
        if (entered == selectedPin)
        {
            pinDialogPanel.SetActive(false);
            ConnectToHost(selectedIp, selectedPort);
        }
        else
        {
            if (pinDialogError != null) pinDialogError.text = "Wrong PIN!";
        }
    }

    private void CreateLobbyLabel(Transform parent, string text, Color color)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 100);
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 50;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        if (gameFont != null) t.font = gameFont;
    }

    // ΔΙΑΧΕΙΡΙΣΗ EVENTS

    private void RegisterEvents()
    {
        NetUtility.C_LOBBY_REFRESH += OnLobbyRefreshClient;
        LobbyDiscovery.OnHostListChanged += PopulateLobby;
    }

    private void UnRegisterEvents()
    {
        NetUtility.C_LOBBY_REFRESH -= OnLobbyRefreshClient;
        LobbyDiscovery.OnHostListChanged -= PopulateLobby;
    }

    private void OnLobbyRefreshClient(NetMessage msg)
    {
        PopulateLobby();
    }

    private void OnDestroy()
    {
        UnRegisterEvents();
    }

    // Καλείται από ChessBord.OnStartGameClient όταν το παιχνίδι ξεκινά
    public void StartGame(int team)
    {
        gameStarted = true;
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }
        MenuAnimator.SetTrigger("inGameMenu");
        waitingPanel.SetActive(false);
        nameInputPanel.SetActive(false);
        hostListPanel.SetActive(false);
        ShowLobbyConnect(false);
        LobbyDiscovery.StopListening();
        HideMenuPanels();
        ChangeCamera(team == 0 ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
    }


    // ΒΟΗΘΗΤΙΚΕΣ ΜΕΘΟΔΟΙ UI
    private GameObject MakePanel(string name, Transform parent, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = bgColor;
        return go;
    }

    private TMP_Text CreateTMPLabel(Transform parent, string text, float fontSize, Color color, Vector2 size)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        if (gameFont != null) t.font = gameFont;
        return t;
    }

    private TMP_InputField CreateInputField(Transform parent, string placeholder, Vector2 size)
    {
        GameObject root = new GameObject("InputField");
        root.transform.SetParent(parent, false);
        root.AddComponent<RectTransform>().sizeDelta = size;
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.20f, 0.14f, 0.14f, 1f);
        TMP_InputField field = root.AddComponent<TMP_InputField>();

        GameObject area = new GameObject("Text Area");
        area.transform.SetParent(root.transform, false);
        RectTransform areaRT = area.AddComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero;
        areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(15, 6);
        areaRT.offsetMax = new Vector2(-15, -6);
        area.AddComponent<RectMask2D>();

        GameObject ph = new GameObject("Placeholder");
        ph.transform.SetParent(area.transform, false);
        StretchFull(ph.AddComponent<RectTransform>());
        TMP_Text phText = ph.AddComponent<TextMeshProUGUI>();
        phText.text = placeholder;
        phText.fontSize = 55;
        phText.color = new Color(0.65f, 0.45f, 0.45f, 1f);
        phText.alignment = TextAlignmentOptions.Midline;
        phText.fontStyle = FontStyles.Italic;
        if (gameFont != null) phText.font = gameFont;

        GameObject txt = new GameObject("Text");
        txt.transform.SetParent(area.transform, false);
        StretchFull(txt.AddComponent<RectTransform>());
        TMP_Text inputText = txt.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 55;
        inputText.color = Color.white;
        inputText.alignment = TextAlignmentOptions.Midline;
        if (gameFont != null) inputText.font = gameFont;

        field.textViewport = areaRT;
        field.textComponent = inputText;
        field.placeholder = phText;
        field.characterLimit = 20;
        return field;
    }

    private void CreateButton(Transform parent, string label, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color * 1.2f;
        cb.pressedColor = color * 0.7f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        StretchFull(txtGO.AddComponent<RectTransform>());
        TMP_Text t = txtGO.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 55;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        if (gameFont != null) t.font = gameFont;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
