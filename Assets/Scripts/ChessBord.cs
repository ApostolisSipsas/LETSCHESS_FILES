
/* Κύριο script σκακιέρας — χειρισμός input (touch + mouse),
   κινήσεις κομματιών, ειδικές κινήσεις, δίκτυο, UI παιχνιδιού.
*/

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class ChessBord : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 2.0f;
    [SerializeField] private float yOffset = 4;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f;
    [SerializeField] private float deathSpacing = 3.0f;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject WinnerScene;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private Button rematchButton;
    private GameObject opponentLeftPanel;

    // Μενού παιχνιδιού 
    private GameObject inGameMenu;
    private Button drawOfferButton;
    private TMP_Text drawOfferBtnText;
    private GameObject drawRespondPanel;  
    private TMP_Text infoYouText;         
    private TMP_Text infoMoveText;        
    private TMP_Text infoTurnText;        
    private GameObject drawWinPanel;      
    private bool drawOfferedByUs = false;
    private int drawOffersCount = 0;

    //sound εφε
    private AudioClip soundMove;
    private AudioClip soundCheck;
    private AudioClip soundCastle;
    private AudioClip soundPromote;
    private AudioSource audioSource;

    [Header("Material/Prefabs")]
    [SerializeField] private GameObject[] Prefabs;
    [SerializeField] private Material[] ColorMaterials;

    private Piece[,] Pieces;
    private Piece currentlyDragging;
    private List<Piece> Deadwhites = new List<Piece>();
    private List<Piece> Deadblacks = new List<Piece>();
    private const int BORD_COUNT_X = 8;
    private const int BORD_COUNT_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    [SerializeField] public Transform deathPlatformWhite; // white player's side (-Z): black pieces go here
    [SerializeField] public Transform deathPlatformBlack; // black player's side (+Z): white pieces go here
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private bool isWhiteTurn;
    private List<Vector2Int> avMoves = new List<Vector2Int>();

    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    private int onlineFirstTeam = 0;
    private bool[] playerRematch = new bool[2];
    private bool onlineDisconnected = false;


    // ============================
    // ΧΕΙΡΙΣΜΟΣ INPUT (TOUCH + MOUSE)
    // ============================
    public void Awake()
    {

    }
    private bool IsInputDown()
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(0).phase == TouchPhase.Began;

        return Input.GetMouseButtonDown(0);
    }

    private bool IsInputUp()
    {
        if (Input.touchCount > 0)
        {
            TouchPhase p = Input.GetTouch(0).phase;
            return p == TouchPhase.Ended || p == TouchPhase.Canceled;
        }

        return Input.GetMouseButtonUp(0);
    }

    private Vector3 GetInputPosition()
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(0).position;

        return Input.mousePosition;
    }

    // =============================


    private void Start()
    {
        // Αρχικοποίηση AudioSource και φόρτωση ήχων από Resources/Sounds
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        soundMove    = Resources.Load<AudioClip>("Sounds/move");
        soundCheck   = Resources.Load<AudioClip>("Sounds/check");
        soundCastle  = Resources.Load<AudioClip>("Sounds/castle");
        soundPromote = Resources.Load<AudioClip>("Sounds/promote");

        AdjustCamera();
        isWhiteTurn = true;
        GenerateBord(tileSize, BORD_COUNT_X, BORD_COUNT_Y);

SpawnAllPieces();
        PositionAllPieces();
        RegisterEvents();
        if (WinnerScene == null || rematchButton == null || rematchIndicator == null || opponentLeftPanel == null) BuildWinnerUI();
        BuildInGameMenu();
    }

    private void BuildWinnerUI()
    {
        TMP_FontAsset font = null;
        var anyTmp = FindFirstObjectByType<TextMeshProUGUI>();
        if (anyTmp != null) font = anyTmp.font;

        // ── Canvas νίκης (screen-space overlay, πάντα στην κορυφή) ───
        GameObject root = new GameObject("WinnerScene");
        Canvas winCanvas = root.AddComponent<Canvas>();
        winCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        winCanvas.sortingOrder = 99;
        root.AddComponent<UnityEngine.UI.CanvasScaler>();
        root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.80f);
        root.SetActive(false);

        // ── Παιδί 0 – κείμενο νίκης Λευκών ──────────────────────────
        GameObject whitePanel = MakeRect("WhiteWins", root.transform,
            new Vector2(0.15f, 0.62f), new Vector2(0.85f, 0.92f), Vector2.zero, Vector2.zero);
        whitePanel.SetActive(false);
        var whiteTxt = AddTMPReturn(whitePanel.transform, "WHITE  WINS!", 90,
            new Color(1f, 0.92f, 0.45f), font);
        whiteTxt.fontStyle = FontStyles.Bold;

        // ── Παιδί 1 – κείμενο νίκης Μαύρων ──────────────────────────
        GameObject blackPanel = MakeRect("BlackWins", root.transform,
            new Vector2(0.15f, 0.62f), new Vector2(0.85f, 0.92f), Vector2.zero, Vector2.zero);
        blackPanel.SetActive(false);
        var blackTxt = AddTMPReturn(blackPanel.transform, "BLACK  WINS!", 90,
            new Color(0.72f, 0.72f, 0.90f), font);
        blackTxt.fontStyle = FontStyles.Bold;

        // ── Δείκτης επαναπαιχνιδιού (παιδί 0 = θέλει, παιδί 1 = αρνήθηκε)
        GameObject indGO = MakeRect("RematchIndicator", root.transform,
            new Vector2(0.1f, 0.50f), new Vector2(0.9f, 0.60f), Vector2.zero, Vector2.zero);

        GameObject wantRematch = MakeRect("WantsRematch", indGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        wantRematch.SetActive(false);
        AddTMPReturn(wantRematch.transform, "Opponent wants a rematch!", 44, new Color(0.3f, 1f, 0.3f), font);

        GameObject declined = MakeRect("Declined", indGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        declined.SetActive(false);
        AddTMPReturn(declined.transform, "Opponent declined rematch.", 44, new Color(1f, 0.35f, 0.35f), font);

        GameObject opponentLeft = MakeRect("OpponentLeft", indGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        opponentLeft.SetActive(false);
        AddTMPReturn(opponentLeft.transform, "Opponent left the game.", 44, new Color(1f, 0.65f, 0.1f), font);

        // ── Panel αποτελέσματος ισοπαλίας ──────────────────────────────────
        GameObject drawPanel = MakeRect("DrawPanel", root.transform,
            new Vector2(0f, 0.6f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);
        drawPanel.SetActive(false);
        AddTMPReturn(drawPanel.transform, "It's a Draw!", 90, new Color(0.9f, 0.85f, 0.3f), font)
            .fontStyle = TMPro.FontStyles.Bold;

        // ── Κουμπί επαναπαιχνιδιού ─────────────────────────────────────
        GameObject rematchGO = MakeRect("RematchBtn", root.transform,
            new Vector2(0.25f, 0.32f), new Vector2(0.75f, 0.47f), Vector2.zero, Vector2.zero);
        Image rematchImg = rematchGO.AddComponent<Image>();
        rematchImg.color = new Color(0.15f, 0.58f, 0.15f);
        Button rb = rematchGO.AddComponent<Button>();
        rb.targetGraphic = rematchImg;
        ColorBlock rbc = rb.colors;
        rbc.highlightedColor = new Color(0.22f, 0.78f, 0.22f);
        rbc.pressedColor     = new Color(0.08f, 0.38f, 0.08f);
        rbc.disabledColor    = new Color(0.25f, 0.35f, 0.25f, 0.5f);
        rb.colors = rbc;
        AddTMPReturn(rematchGO.transform, "REMATCH", 60, Color.white, font);
        rb.onClick.AddListener(OnRematchButton);

        // ── Κουμπί κύριου μενού ────────────────────────────────────────
        GameObject menuGO = MakeRect("MenuBtn", root.transform,
            new Vector2(0.25f, 0.14f), new Vector2(0.75f, 0.29f), Vector2.zero, Vector2.zero);
        Image menuImg = menuGO.AddComponent<Image>();
        menuImg.color = new Color(0.55f, 0.15f, 0.15f);
        Button mb = menuGO.AddComponent<Button>();
        mb.targetGraphic = menuImg;
        ColorBlock mbc = mb.colors;
        mbc.highlightedColor = new Color(0.75f, 0.22f, 0.22f);
        mbc.pressedColor     = new Color(0.35f, 0.08f, 0.08f);
        mb.colors = mbc;
        AddTMPReturn(menuGO.transform, "MAIN MENU", 60, Color.white, font);
        mb.onClick.AddListener(OnMenuButton);

        // ── Ανάθεση αναφορών στα πεδία ──────────────────────────────────────
        WinnerScene        = root;
        rematchIndicator   = indGO.transform;
        rematchButton      = rb;
        opponentLeftPanel  = opponentLeft;
        drawWinPanel       = drawPanel;
    }

    private void BuildInGameMenu()
    {
        TMP_FontAsset font = null;
        TMP_Text any = FindFirstObjectByType<TMP_Text>();
        if (any != null) font = any.font;

        // Ένα Canvas — και οι δύο μπάρες εδώ ώστε ένα SetActive να τα ελέγχει όλα
        GameObject root = new GameObject("InGameMenu");
        Canvas c = root.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 5;
        UnityEngine.UI.CanvasScaler cs = root.AddComponent<UnityEngine.UI.CanvasScaler>();
        cs.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080, 1920);
        cs.screenMatchMode     = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        Rect  safe      = Screen.safeArea;
        float safeBot   = safe.yMin  / Screen.height;
        float safeTop   = safe.yMax  / Screen.height;
        float topBarH   = 0.10f;   // top info bar height
        float botBarH   = 0.11f;   // bottom button bar height
        Color barBg     = new Color(0.07f, 0.07f, 0.11f, 0.95f);
        Color cellBg    = new Color(0.12f, 0.12f, 0.20f, 0.88f);
        Color divColor  = new Color(0.30f, 0.30f, 0.45f, 0.70f);

        // ── Πάνω μπάρα πληροφοριών ──────────────────────────────────────────────
        float topBot = safeTop - topBarH;
        GameObject topBar = MakeRect("TopBar", root.transform,
            new Vector2(0f, topBot), new Vector2(1f, safeTop), Vector2.zero, Vector2.zero);
        topBar.AddComponent<UnityEngine.UI.Image>().color = barBg;

        // Τρία ισόποσα κελιά χωρισμένα από λεπτές κατακόρυφες διαχωριστικές γραμμές
        var youCell  = MakeRect("YouCell",  topBar.transform, new Vector2(0.004f, 0.06f), new Vector2(0.330f, 0.94f), Vector2.zero, Vector2.zero);
        var moveCell = MakeRect("MoveCell", topBar.transform, new Vector2(0.337f, 0.06f), new Vector2(0.663f, 0.94f), Vector2.zero, Vector2.zero);
        var turnCell = MakeRect("TurnCell", topBar.transform, new Vector2(0.670f, 0.06f), new Vector2(0.996f, 0.94f), Vector2.zero, Vector2.zero);
        youCell .AddComponent<UnityEngine.UI.Image>().color = cellBg;
        moveCell.AddComponent<UnityEngine.UI.Image>().color = cellBg;
        turnCell.AddComponent<UnityEngine.UI.Image>().color = cellBg;
        // Κατακόρυφες διαχωριστικές γραμμές
        MakeRect("Div1", topBar.transform, new Vector2(0.332f, 0f), new Vector2(0.336f, 1f), Vector2.zero, Vector2.zero)
            .AddComponent<UnityEngine.UI.Image>().color = divColor;
        MakeRect("Div2", topBar.transform, new Vector2(0.664f, 0f), new Vector2(0.668f, 1f), Vector2.zero, Vector2.zero)
            .AddComponent<UnityEngine.UI.Image>().color = divColor;

        infoYouText  = AddTMPReturn(youCell .transform, "YOU: WHITE", 38, new Color(0.85f, 0.85f, 0.85f), font);
        infoMoveText = AddTMPReturn(moveCell.transform, "Move 1",     38, new Color(0.70f, 0.70f, 0.70f), font);
        infoTurnText = AddTMPReturn(turnCell.transform, "YOUR TURN",  38, new Color(0.35f, 1.00f, 0.35f), font);

        // ── Κάτω μπάρα ενεργειών ─────────────────────────────────────────
        GameObject botBar = MakeRect("BotBar", root.transform,
            new Vector2(0f, safeBot), new Vector2(1f, safeBot + botBarH), Vector2.zero, Vector2.zero);
        botBar.AddComponent<UnityEngine.UI.Image>().color = barBg;

        GameObject drawGO = MakeRect("DrawBtn", botBar.transform,
            new Vector2(0.02f, 0.08f), new Vector2(0.49f, 0.92f), Vector2.zero, Vector2.zero);
        drawGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.50f, 0.15f);
        Button db = drawGO.AddComponent<Button>();
        db.targetGraphic = drawGO.GetComponent<UnityEngine.UI.Image>();
        ColorBlock dbc = db.colors;
        dbc.highlightedColor = new Color(0.22f, 0.70f, 0.22f);
        dbc.pressedColor     = new Color(0.08f, 0.30f, 0.08f);
        dbc.disabledColor    = new Color(0.25f, 0.35f, 0.25f, 0.5f);
        db.colors = dbc;
        drawOfferBtnText = AddTMPReturn(drawGO.transform, "OFFER DRAW", 46, Color.white, font);
        db.onClick.AddListener(OnDrawButton);

        GameObject leaveGO = MakeRect("LeaveBtn", botBar.transform,
            new Vector2(0.51f, 0.08f), new Vector2(0.98f, 0.92f), Vector2.zero, Vector2.zero);
        leaveGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.55f, 0.15f, 0.15f);
        Button lb = leaveGO.AddComponent<Button>();
        lb.targetGraphic = leaveGO.GetComponent<UnityEngine.UI.Image>();
        ColorBlock lbc = lb.colors;
        lbc.highlightedColor = new Color(0.75f, 0.22f, 0.22f);
        lbc.pressedColor     = new Color(0.35f, 0.08f, 0.08f);
        lb.colors = lbc;
        AddTMPReturn(leaveGO.transform, "LEAVE", 46, Color.white, font);
        lb.onClick.AddListener(OnMenuButton);

        // ── Panel απόκρισης ισοπαλίας (επιπλέει πάνω από κάτω μπάρα) ──────────────
        float respondBot = safeBot + botBarH + 0.015f;
        GameObject respondRoot = MakeRect("DrawRespondPanel", root.transform,
            new Vector2(0.05f, respondBot), new Vector2(0.95f, respondBot + 0.24f), Vector2.zero, Vector2.zero);
        respondRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.10f, 0.16f, 0.97f);
        respondRoot.SetActive(false);

        GameObject msgGO = MakeRect("DrawMsg", respondRoot.transform,
            new Vector2(0f, 0.55f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        AddTMPReturn(msgGO.transform, "Opponent offers a draw!", 50, Color.white, font);

        GameObject acceptGO = MakeRect("AcceptBtn", respondRoot.transform,
            new Vector2(0.05f, 0.10f), new Vector2(0.47f, 0.54f), Vector2.zero, Vector2.zero);
        acceptGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.55f, 0.15f);
        Button ab = acceptGO.AddComponent<Button>();
        ab.targetGraphic = acceptGO.GetComponent<UnityEngine.UI.Image>();
        AddTMPReturn(acceptGO.transform, "ACCEPT", 46, Color.white, font);
        ab.onClick.AddListener(OnAcceptDraw);

        GameObject declineGO = MakeRect("DeclineBtn", respondRoot.transform,
            new Vector2(0.53f, 0.10f), new Vector2(0.95f, 0.54f), Vector2.zero, Vector2.zero);
        declineGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.55f, 0.15f, 0.15f);
        Button decb = declineGO.AddComponent<Button>();
        decb.targetGraphic = declineGO.GetComponent<UnityEngine.UI.Image>();
        AddTMPReturn(declineGO.transform, "DECLINE", 46, Color.white, font);
        decb.onClick.AddListener(OnDeclineDraw);

        inGameMenu       = root;
        drawOfferButton  = db;
        drawRespondPanel = respondRoot;
        root.SetActive(false);
    }

    private void UpdateInGameInfo()
    {
        if (infoYouText == null) return;

        // Label "ΕΣΥ" — στο τοπικό παιχνίδι δείχνει τον τρέχοντα παίκτη, online τη σταθερή ομάδα σου
        if (localGame)
        {
            infoYouText.text  = isWhiteTurn ? "YOU: ● WHITE" : "YOU: ● BLACK";
            infoYouText.color = isWhiteTurn ? Color.white : new Color(0.55f, 0.85f, 1f);
        }
        else
        {
            infoYouText.text  = currentTeam == 0 ? "YOU: ● WHITE" : "YOU: ● BLACK";
            infoYouText.color = currentTeam == 0 ? Color.white : new Color(0.55f, 0.85f, 1f);
        }

        infoMoveText.text = $"Move {moveList.Count + 1}";

        // Κατάσταση σειράς / σαχ
        bool myTurn  = localGame || (isWhiteTurn ? currentTeam == 0 : currentTeam == 1);
        bool inCheck = IsKingInCheck(isWhiteTurn ? 0 : 1);

        if (inCheck)
        {
            infoTurnText.text  = "CHECK!";
            infoTurnText.color = new Color(1f, 0.25f, 0.25f);
        }
        else if (myTurn)
        {
            infoTurnText.text  = "YOUR TURN";
            infoTurnText.color = new Color(0.35f, 1f, 0.35f);
        }
        else
        {
            infoTurnText.text  = "OPP. TURN";
            infoTurnText.color = new Color(0.55f, 0.55f, 0.55f);
        }
    }

    private bool IsKingInCheck(int team)
    {
        Piece king = null;
        var attackers = new List<Piece>();
        for (int x = 0; x < BORD_COUNT_X; x++)
            for (int y = 0; y < BORD_COUNT_Y; y++)
            {
                if (Pieces[x, y] == null) continue;
                if (Pieces[x, y].Color == team) { if (Pieces[x, y].type == PieceType.King) king = Pieces[x, y]; }
                else attackers.Add(Pieces[x, y]);
            }
        if (king == null) return false;
        var kingPos = new Vector2Int(king.posX, king.posY);
        foreach (var a in attackers)
            if (a.GetAvailavleMoves(ref Pieces, BORD_COUNT_X, BORD_COUNT_Y).Contains(kingPos))
                return true;
        return false;
    }

    private void ShowInGameMenu()
    {
        if (inGameMenu != null) inGameMenu.SetActive(true);
        UpdateInGameInfo();
    }

    private void HideInGameMenu()
    {
        if (inGameMenu != null) inGameMenu.SetActive(false);
    }

    // Επιστρέφει το TMP component ώστε ο καλών να μπορεί να τροποποιήσει το στυλ
    private static TextMeshProUGUI AddTMPReturn(Transform parent, string text, float size, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.color     = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        if (font != null) t.font = font;
        return t;
    }

    private static GameObject MakeRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
        return go;
    }

    private static void AddTMP(Transform parent, string text, float size, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.color     = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        if (font != null) t.font = font;
    }

    void AdjustCamera()
    {
        Camera cam = Camera.main;

        float aspectRatio = (float)Screen.width / Screen.height;
        float t = Mathf.InverseLerp(1.3f, 2.4f, aspectRatio);

        cam.fieldOfView = Mathf.Lerp(50f, 70f, t);

        float height = Mathf.Lerp(20f, 28f, t);
        float distance = Mathf.Lerp(25f, 33f, t);

        float sideOffset = 10f;

        cam.transform.position = new Vector3(sideOffset, height, -distance);

        float angle = Mathf.Lerp(47f, 53f, t);
        cam.transform.rotation = Quaternion.Euler(angle, -25f, 0f);

        cam.aspect = aspectRatio;
    }


    private void Update()
    {
        if (!currentCamera)
        {
            currentCamera = GameUI.Instance != null ? GameUI.Instance.GetCurrentCamera() : Camera.main;
            return;
        }

        Ray ray = currentCamera.ScreenPointToRay(GetInputPosition());

        // Δημιουργούμε ένα επίπεδο στο ύψος του board (yOffset)
        Plane boardPlane = new Plane(Vector3.up, Vector3.up * yOffset);

        if (boardPlane.Raycast(ray, out float enter))
        {
            // Σημείο που χτυπήθηκε στο board
            Vector3 hitPoint = ray.GetPoint(enter);

            // Μετατροπή σε grid indices
            int x = Mathf.FloorToInt((hitPoint.x + bounds.x) / tileSize);
            int y = Mathf.FloorToInt((hitPoint.z + bounds.z) / tileSize);

            // Έλεγχος αν είναι μέσα στα όρια του board
            if (x >= 0 && x < BORD_COUNT_X && y >= 0 && y < BORD_COUNT_Y)
            {
                Vector2Int hitPosition = new Vector2Int(x, y);

                // --- Hover handling ---
                if (currentHover != hitPosition)
                {
                    if (currentHover != -Vector2Int.one)
                    {
                        tiles[currentHover.x, currentHover.y].gameObject.layer =
                            (ValidMoveContainer(ref avMoves, currentHover))
                            ? LayerMask.NameToLayer("Highlight")
                            : LayerMask.NameToLayer("Tile");
                    }

                    currentHover = hitPosition;
                    tiles[hitPosition.x, hitPosition.y].gameObject.layer = LayerMask.NameToLayer("Hover");
                }

                // --- Input Down ---
                if (IsInputDown())
                {
                    Piece p = Pieces[hitPosition.x, hitPosition.y];
                    if (p != null)
                    {
                        bool myTurn = localGame
                            ? ((p.Color == 0 && isWhiteTurn) || (p.Color == 1 && !isWhiteTurn))
                            : ((p.Color == 0 && isWhiteTurn && currentTeam == 0) || (p.Color == 1 && !isWhiteTurn && currentTeam == 1));
                        if (myTurn)
                        {
                            currentlyDragging = p;
                            avMoves = currentlyDragging.GetAvailavleMoves(ref Pieces, BORD_COUNT_X, BORD_COUNT_Y);
                            specialMove = currentlyDragging.GetSpecialMove(ref Pieces, ref moveList, ref avMoves);

                            PreventCheck();
                            ShowAvMoves();
                        }
                    }
                }

                // --- Input Up / Move ---
                if (currentlyDragging != null && IsInputUp())
                {
                    Vector2Int previousPosition = new Vector2Int(currentlyDragging.posX, currentlyDragging.posY);

                    if (ValidMoveContainer(ref avMoves,new Vector2Int(hitPosition.x,hitPosition.y)))
                    {
                        int sendingTeam = currentTeam;
                        MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                        NetMakeMove mm = new NetMakeMove();
                        mm.origX = previousPosition.x;
                        mm.origY = previousPosition.y;
                        mm.destX = hitPosition.x;
                        mm.destY = hitPosition.y;
                        mm.team = sendingTeam;
                        Debug.Log($"[SEND] team={sendingTeam} orig=({previousPosition.x},{previousPosition.y}) dest=({hitPosition.x},{hitPosition.y}) localGame={localGame}");
                        Client.Instance.SendToServer(mm);
                    }
                    else
                    {
                        currentlyDragging.setPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                        currentlyDragging = null;
                        HideAvMoves();
                    }
                }
            }
            else // αν το hitPoint είναι εκτός board
            {
                if (currentHover != -Vector2Int.one)
                {
                    tiles[currentHover.x, currentHover.y].gameObject.layer =
                        (ValidMoveContainer(ref avMoves, currentHover))
                        ? LayerMask.NameToLayer("Highlight")
                        : LayerMask.NameToLayer("Tile");
                    currentHover = -Vector2Int.one;
                }

                if (currentlyDragging != null && IsInputUp())
                {
                    currentlyDragging.setPosition(GetTileCenter(currentlyDragging.posX, currentlyDragging.posY));
                    currentlyDragging = null;
                    HideAvMoves();
                }
            }

            // --- Dragging visual update ---
            if (currentlyDragging != null)
            {
                currentlyDragging.setPosition(hitPoint + Vector3.up * dragOffset);
            }
        }
        else // αν το ray δεν χτύπησε κανένα plane
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].gameObject.layer =
                    (ValidMoveContainer(ref avMoves, currentHover))
                    ? LayerMask.NameToLayer("Highlight")
                    : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if (currentlyDragging != null && IsInputUp())
            {
                currentlyDragging.setPosition(GetTileCenter(currentlyDragging.posX, currentlyDragging.posY));
                currentlyDragging = null;
                HideAvMoves();
            }
        }
    }

    // Εύρεση ευρετηρίου τετραγώνου βάσει GameObject
    private Vector2Int LookUpTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < BORD_COUNT_X; x++)
            for (int y = 0; y < BORD_COUNT_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);
        return -Vector2Int.one;

    }
    // Αναπαραγωγή κατάλληλου ήχου βάσει τύπου κίνησης
    private void PlayMoveSound()
    {
        if (audioSource == null) return;

        // Προτεραιότητα: σαχ > ροκέ > προαγωγή > απλή κίνηση
        bool opponentInCheck = IsKingInCheck(isWhiteTurn ? 0 : 1);

        AudioClip clip;
        if (opponentInCheck && soundCheck != null)
            clip = soundCheck;
        else if (specialMove == SpecialMove.Castling && soundCastle != null)
            clip = soundCastle;
        else if (specialMove == SpecialMove.Promotion && soundPromote != null)
            clip = soundPromote;
        else
            clip = soundMove;

        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void MoveTo(int origX,int origY, int x, int y)
    {

        Piece cp = Pieces[origX, origY];
        Vector2Int previousPosition = new Vector2Int(origX, origY);

        // αν δεν υπαρχει πίονι στην θέση που πάμε να παίξουμε
        if (Pieces[x, y] != null)
        {
            Piece TargetPiece = Pieces[x, y];
            if (cp.Color == TargetPiece.Color)
                return ;

            if (TargetPiece.Color == 0)
            {
                if (TargetPiece.type == PieceType.King)
                {
                    CheckMate(1);
                }
                Deadwhites.Add(TargetPiece);
                PlaceDeadPiece(TargetPiece, deathPlatformBlack, Deadwhites.Count - 1);
            }
            else
            {
                if (TargetPiece.type == PieceType.King)
                {
                    CheckMate(0);
                }
                Deadblacks.Add(TargetPiece);
                PlaceDeadPiece(TargetPiece, deathPlatformWhite, Deadblacks.Count - 1);
            }
        }

        Pieces[x, y] = cp;
        Pieces[previousPosition.x, previousPosition.y] = null;
        PositionSingePiece(x, y);
        isWhiteTurn = !isWhiteTurn;
        if (localGame)
        {
            currentTeam = (currentTeam == 0) ? 1 : 0;
        }
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });
        ProssesSpecialMove();
        if(currentlyDragging)
            currentlyDragging = null;
        HideAvMoves();
        if (CheckForCheckmate())
        {
            Debug.Log("checkmate");
            CheckMate(cp.Color);
        }
        else
        {
            // αν μένουν μονο οι βασιλίαδες ισοπαλία
            int remaining = 0;
            for (int i = 0; i < BORD_COUNT_X; i++)
                for (int j = 0; j < BORD_COUNT_Y; j++)
                    if (Pieces[i, j] != null) remaining++;
            if (remaining == 2)
                DisplayDraw();
        }
        UpdateInGameInfo();
        PlayMoveSound();

        return ;
    }

    private bool ValidMoveContainer(ref List<Vector2Int> moves, Vector2 pos)
    {
        for (int i = 0; i < moves.Count; i++)
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;
        return false;
    }
    // Δημιουργία σκακιέρας
    private void GenerateBord(float bordSize, int bordCountX, int bordCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((bordCountX / 2) * tileSize, 0, (bordCountX / 2) * tileSize) + boardCenter;
        tiles = new GameObject[bordCountX, bordCountY];
        for (int i = 0; i < bordCountX; i++)
            for (int j = 0; j < bordCountY; j++)
                tiles[i, j] = GenerateSignleTile(bordSize, i, j);

    }

    private void PlaceDeadPiece(Piece piece, Transform platform, int idx)
    {
        piece.setScale(Vector3.one * 0.9f, true);
        piece.setPosition(GetDeadPosition(platform, idx));
    }

    private Vector3 GetDeadPosition(Transform platform, int idx)
    {
        if (platform == null) return Vector3.zero;
        int col = idx % 8;
        int row = idx / 8;
        float lx = -0.5f + (col + 0.5f) / 8f;
        float ly = -0.5f + (row + 0.5f) / 2f;
        float lz = 0.5f;
        return platform.TransformPoint(lx, ly, lz);
    }
    private GameObject GenerateSignleTile(float tileSize, int x, int y)
    {
        GameObject tile = new GameObject(string.Format("X:{0},Y:{1}", x, y));
        tile.transform.parent = transform;
        Mesh mesh = new Mesh();
        tile.AddComponent<MeshFilter>().mesh = mesh;
        tile.AddComponent<MeshRenderer>().material = tileMaterial;
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, 0, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, 0, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, 0, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, 0, (y + 1) * tileSize) - bounds;
        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tile.layer = LayerMask.NameToLayer("Tile");
        tile.AddComponent<BoxCollider>();
        return tile;
    }

    // spown 1 πίονι
    private Piece SpawnSinglePiece(PieceType type, int Color)
    {
        Piece p = Instantiate(Prefabs[(int)type - 1], transform).GetComponent<Piece>();
        p.transform.localScale = new Vector3(1, 1, 1);
        p.transform.rotation = new Quaternion(0, 0, 0, 0);
        p.type = type;
        p.Color = Color;
        p.GetComponent<MeshRenderer>().material = ColorMaterials[Color];
        return p;
    }

    // Επεξεργασία ειδικών κινήσεων 
    private void ProssesSpecialMove()
    {
        if (specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            Piece myPown = Pieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition= moveList[moveList.Count - 2];
            Piece enemyPawn = Pieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if (myPown.posX== enemyPawn.posX)
            {
                if (myPown.posY == enemyPawn.posY-1 || myPown.posY == enemyPawn.posY+1)
                {
                    if (enemyPawn.Color ==0)
                    {
                        Deadblacks.Add(enemyPawn);
                        PlaceDeadPiece(enemyPawn, deathPlatformWhite, Deadblacks.Count - 1);
                    }
                    else
                    {
                        Deadwhites.Add(enemyPawn);
                        PlaceDeadPiece(enemyPawn, deathPlatformBlack, Deadwhites.Count - 1);
                    }
                }
            }


        }
        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            Piece targetPawn = Pieces[lastMove[1].x, lastMove[1].y];

            if (targetPawn.type == PieceType.Pawn)
            {
                // prommotion white
                if (targetPawn.Color == 0 && lastMove[1].y == 7)
                {
                    Piece newQueen = SpawnSinglePiece(PieceType.Queen, 0);
                    newQueen.transform.position = targetPawn.transform.position;

                    Destroy(targetPawn.gameObject);
                    Pieces[lastMove[1].x, lastMove[1].y] = newQueen;

                    PositionSingePiece(lastMove[1].x, lastMove[1].y);
                }

                // wromotion black
                if (targetPawn.Color == 1 && lastMove[1].y == 0)
                {
                    Piece newQueen = SpawnSinglePiece(PieceType.Queen, 1);
                    newQueen.transform.position = targetPawn.transform.position;

                    Destroy(targetPawn.gameObject);
                    Pieces[lastMove[1].x, lastMove[1].y] = newQueen;

                    PositionSingePiece(lastMove[1].x, lastMove[1].y);
                }
            }
        }
        if (specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            // Castling αριστερός πργος
            if (lastMove[1].x == 2 )
            {
                if (lastMove[1].y==0)
                {
                    Piece cp = Pieces[0, 0];
                    Pieces[3, 0] = cp;
                    PositionSingePiece(3, 0);
                    Pieces[0, 0] = null;
                }
                else if (lastMove[1].y ==7 )
                {
                    Piece cp = Pieces[0, 7];
                    Pieces[3, 7] = cp;
                    PositionSingePiece(3, 7 );
                    Pieces[0, 7] = null;
                }

            }
            // Castling δεξιoς πύργος
            else if (lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0)
                {
                    Piece cp = Pieces[7, 0];
                    Pieces[5, 0] = cp;
                    PositionSingePiece(5, 0);
                    Pieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    Piece cp = Pieces[7, 7];
                    Pieces[5, 7] = cp;
                    PositionSingePiece(5, 7);
                    Pieces[5, 7] = null;
                }
            }
        }
    }
    private void PreventCheck()
    {
        Piece targetKing = null;
        for (int  x = 0;   x < BORD_COUNT_X;  x++)
        {
            for (int y = 0; y < BORD_COUNT_Y; y++)
            {
                if (Pieces[x,y] != null)
                    if (Pieces[x, y].type == PieceType.King)
                        if (Pieces[x, y].Color == currentlyDragging.Color)
                            targetKing = Pieces[x, y];
            }
        }
        SimulateMoveForSinglePiece(currentlyDragging, ref avMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(Piece cp,ref List<Vector2Int> moves ,Piece targetKing)
    {
        int actualX = cp.posX;
        int actualY = cp.posY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();
        // Προσομοίωση  κίνησεών
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.posX, targetKing.posY);
            if (cp.type == PieceType.King)
                kingPositionThisSim = new Vector2Int(simX, simY);
            // Αντιγραφη σκακιέρας 
            Piece[,] simulationBord = new Piece[BORD_COUNT_X, BORD_COUNT_Y];
            List<Piece> simAttacking = new List<Piece>();
            for (int x = 0; x < BORD_COUNT_X; x++)
            {
                for (int y = 0; y < BORD_COUNT_Y; y++)
                {
                    if (Pieces[x,y] != null)
                    {
                        simulationBord[x, y] = Pieces[x, y];
                        if (simulationBord[x,y].Color != cp.Color)
                        {
                            simAttacking.Add(simulationBord[x, y]);
                        }
                    }
                }
            }

            // Εκτελεση της κίνησης στη προσομοιωση
            simulationBord[actualX, actualY] = null;
            cp.posX = simX;
            cp.posY = simY;
            simulationBord[simX, simY] = cp;

            var deadPiece = simAttacking.Find(c => c.posX == simX && c.posY == simY);
            if (deadPiece != null)
                simAttacking.Remove(deadPiece);
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a =0; a<simAttacking.Count; a++)
            {
                var pieceMoves = simAttacking[a].GetAvailavleMoves(ref simulationBord, BORD_COUNT_X, BORD_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count ; b++)
                {
                    simMoves.Add(pieceMoves[b]);
                }
            }
            // Αν ο βασιλιάς παραμένει σε κίνδυνο μετά από αυτή την κίνηση, την αφαιρούμε
            if (ValidMoveContainer(ref simMoves, kingPositionThisSim))
            {
                movesToRemove.Add(moves[i]);
            }

            // Επαναφορά πραγματικής θέσης κομματιού μετά την προσομοίωση
            cp.posX = actualX;
            cp.posY = actualY;
        }


        // Αφαίρεση μη έγκυρων κινήσεων από τη λίστα διαθέσιμων κινήσεων
        for (int i = 0; i < movesToRemove.Count; i++)
            moves.Remove(movesToRemove[i]);

    }
    private bool CheckForCheckmate()
    {

        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (Pieces[lastMove[1].x, lastMove[1].y].Color == 0) ? 1 : 0;
        List<Piece> attackingPieces = new List<Piece>();
        List<Piece> defendingPieces = new List<Piece>();
        Piece targetKing = null;
        for (int x = 0; x < BORD_COUNT_X; x++)
            for (int y = 0; y < BORD_COUNT_Y; y++)
                if (Pieces[x, y] != null)
                {
                    if (Pieces[x, y].Color == targetTeam)
                    {
                        defendingPieces.Add(Pieces[x, y]);
                        if (Pieces[x, y].type == PieceType.King)
                            targetKing = Pieces[x, y];
                    }
                    else
                    {
                        attackingPieces.Add(Pieces[x, y]);
                    }
                }

        // Ελένχουμε αν ο βασιλιάς δέχεται επίθεση 
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i =0; i<attackingPieces.Count;i++)
        {
                var pieceMoves = attackingPieces[i].GetAvailavleMoves(ref Pieces, BORD_COUNT_X, BORD_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                {


                currentAvailableMoves.Add(pieceMoves[b]);
                }

        }
        // σαχ
        if(ValidMoveContainer(ref currentAvailableMoves , new Vector2Int(targetKing.posX,targetKing.posY)))
        {
            Debug.Log("checFOR chekkmate");
            // διαθέσιμες κίνησεις για να βγούμε από το σαχ
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defmoves = defendingPieces[i].GetAvailavleMoves(ref Pieces, BORD_COUNT_X, BORD_COUNT_Y);
                SimulateMoveForSinglePiece(defendingPieces[i], ref defmoves, targetKing);
                if (defmoves.Count != 0)
                    return false;

            }
            return true;
        }
        return false;

    }
    // Ματ 
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winner)
    {
        HideInGameMenu();
        WinnerScene.SetActive(true);
        WinnerScene.transform.GetChild(winner).gameObject.SetActive(true);
    }

    private void DisplayDraw()
    {
        HideInGameMenu();
        WinnerScene.SetActive(true);
        if (drawWinPanel != null) drawWinPanel.SetActive(true);
    }
    public void OnRematchButton()
    {
        if (localGame)
        {
            GameReset();
            // local game
            if (drawOfferButton != null) drawOfferButton.interactable = false;
            StartCoroutine(ShowInGameMenuNextFrame());
        }
        else
        {
            NetRematch rm = new NetRematch();
            rm.team = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);
        }
    }
    public void GameReset() {
        rematchButton.interactable = true;
        for (int i = 0; i < rematchIndicator.childCount; i++)
            rematchIndicator.GetChild(i).gameObject.SetActive(false);
        WinnerScene.transform.GetChild(0).gameObject.SetActive(false);
        WinnerScene.transform.GetChild(1).gameObject.SetActive(false);
        if (drawWinPanel != null) drawWinPanel.SetActive(false);
        WinnerScene.SetActive(false);
        // respown chess bord
        onlineDisconnected = false;
        drawOfferedByUs = false;
        drawOffersCount = 0;
        StopCoroutine("ResetDrawButtonAfterDelay");
        if (drawRespondPanel != null) drawRespondPanel.SetActive(false);
        if (drawOfferButton != null) { drawOfferButton.interactable = true; drawOfferBtnText.text = "OFFER DRAW"; }
        HideInGameMenu();
        currentlyDragging = null;
        moveList.Clear();
        avMoves.Clear();
        playerRematch[0] = false;
        playerRematch[1] = false;
        // init πινακα (διαγραφή κπιωνίβν)
        for (int x = 0; x < BORD_COUNT_X; x++)
        {
            for (int y = 0; y < BORD_COUNT_Y; y++)
            {
                if (Pieces[x, y] != null)
                {
                    Destroy(Pieces[x, y].gameObject);
                    Pieces[x, y] = null;
                }
            }
        }

        // Καταστροφή  κομματιών
        foreach (Piece p in Deadwhites)
            if (p != null) Destroy(p.gameObject);
        foreach (Piece p in Deadblacks)
            if (p != null) Destroy(p.gameObject);

        Deadwhites.Clear();
        Deadblacks.Clear();
        Pieces = new Piece[BORD_COUNT_X, BORD_COUNT_Y];
        // Respawn
        SpawnAllPieces();
        PositionAllPieces();

        isWhiteTurn = true;
    }
    public void OnMenuButton()
    {
        if (!localGame && Client.Instance.IsActive)
        {
            NetRematch rm = new NetRematch();
            rm.team = currentTeam;
            rm.wantRematch = 0;
            Client.Instance.SendToServer(rm);
        }

        GameReset();
        GameUI.Instance.OnLeaveFromGameMenu();
        playerCount = -1;
        currentTeam = -1;
        Client.Instance.ShutDown();
        Server.Instance.ShutDown();
    }
    private void PositionAllPieces()
    {
        for (int  x= 0;  x< BORD_COUNT_X; x++)
        {
            for (int y = 0; y < BORD_COUNT_Y; y++)
            {
                if (Pieces[x,y]!= null)
                {
                    PositionSingePiece(x, y, true);
                }
            }
        }
    }
    private void PositionSingePiece(int x ,int y,bool force = false)
    {
        Pieces[x, y].posX = x;
        Pieces[x, y].posY = y;
        Pieces[x, y].setPosition(GetTileCenter(x, y),force);
    }
    private Vector3 GetTileCenter(int x,int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }
    private void SpawnAllPieces()
    {
        Pieces = new Piece[BORD_COUNT_X, BORD_COUNT_Y];


        int White = 0;int Black = 1;


        // init λευκα 
        Pieces[0, 0] = SpawnSinglePiece(PieceType.Rook, White);
        Pieces[1, 0] = SpawnSinglePiece(PieceType.Knight, White);
        Pieces[2, 0] = SpawnSinglePiece(PieceType.Bishop, White);
        Pieces[4, 0] = SpawnSinglePiece(PieceType.King, White);
        Pieces[3, 0] = SpawnSinglePiece(PieceType.Queen, White);
        Pieces[5, 0] = SpawnSinglePiece(PieceType.Bishop, White);
        Pieces[6, 0] = SpawnSinglePiece(PieceType.Knight, White);
        Pieces[7, 0] = SpawnSinglePiece(PieceType.Rook, White);
        for (int i = 0; i < BORD_COUNT_X; i++)
        {
            Pieces[i, 1] = SpawnSinglePiece(PieceType.Pawn, White);
            Pieces[i, 1].transform.localScale = new Vector3(1f, 1f, 1f);
        }
        //init μαυρα
        Pieces[0, 7] = SpawnSinglePiece(PieceType.Rook, Black);
        Pieces[1, 7] = SpawnSinglePiece(PieceType.Knight, Black);
        Pieces[2, 7] = SpawnSinglePiece(PieceType.Bishop, Black);
        Pieces[4, 7] = SpawnSinglePiece(PieceType.King, Black);
        Pieces[3, 7] = SpawnSinglePiece(PieceType.Queen, Black);
        Pieces[5, 7] = SpawnSinglePiece(PieceType.Bishop, Black);
        Pieces[6, 7] = SpawnSinglePiece(PieceType.Knight, Black);
        Pieces[7, 7] = SpawnSinglePiece(PieceType.Rook, Black);
       for (int i = 0; i < BORD_COUNT_X; i++)
        {
            Pieces[i, 6] = SpawnSinglePiece(PieceType.Pawn, Black);
            Pieces[i, 6].transform.localScale = new Vector3(1f, 1f, 1f);
        }


    }
    //εμφανιση available moves
    private void ShowAvMoves()
    {
        for (int i = 0; i < avMoves.Count; i++)
        {
            tiles[avMoves[i].x, avMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void HideAvMoves()
    {
        for (int i = 0; i < avMoves.Count; i++)
        {
            tiles[avMoves[i].x, avMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        avMoves.Clear();
    }

    private void RegisterEvents()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        GameUI.Instance.SetLocalGame += OnSetLocalGame;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.S_REMATCH += OnRematchServer;
        NetUtility.C_REMATCH += OnRematchClient;
        NetUtility.S_DRAW    += OnDrawServer;
        NetUtility.C_DRAW    += OnDrawClient;
        Client.Instance.connectionDropped += OnConnectionDropped;
    }

    private void UnRegisterEvents()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;
        if (GameUI.Instance != null)  GameUI.Instance.SetLocalGame -= OnSetLocalGame;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.S_REMATCH -= OnRematchServer;
        NetUtility.C_REMATCH -= OnRematchClient;
        NetUtility.S_DRAW    -= OnDrawServer;
        NetUtility.C_DRAW    -= OnDrawClient;
        if (Client.Instance != null)  Client.Instance.connectionDropped -= OnConnectionDropped;
    }

    private void OnDestroy()
    {
        UnRegisterEvents();
    }
    private void OnWelcomeServer(NetMessage msg , NetworkConnection cnn)
    {
        NetWelcome nw = msg as NetWelcome;

        ++playerCount;
        if (playerCount == 0)
            onlineFirstTeam = UnityEngine.Random.Range(0, 2);
        nw.AssignedTeam = (playerCount == 0) ? onlineFirstTeam : 1 - onlineFirstTeam;
        Debug.Log(nw.AssignedTeam);
        Server.Instance.SendToClient(cnn, nw);
        Debug.Log("hello server");
        if (playerCount == 1)
        {
            LobbyDiscovery.StopBroadcasting();
            Debug.Log("startgame drodcast");
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn)
    {
        NetMakeMove mm = msg as NetMakeMove;
        Debug.Log($"[SERVER] Received move team={mm.team} orig=({mm.origX},{mm.origY}) dest=({mm.destX},{mm.destY}) — broadcasting except sender");
        Server.Instance.BroadcastExcept(msg, cnn);
    }
    private void OnWelcomeClient(NetMessage msg)
    {
        NetWelcome nw = msg as NetWelcome;
        currentTeam = nw.AssignedTeam;
        Debug.Log($"My Assinged team is :{nw.AssignedTeam}");

        if (localGame)
        {
            currentTeam = 0;
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnStartGameClient(NetMessage msg)
    {
        Debug.Log("STEP BEFORE CAMERA CHANGE");
        GameUI.Instance.StartGame(currentTeam);
        currentCamera = null;
        drawOfferedByUs = false;
        drawOffersCount = 0;
        if (drawOfferButton != null) drawOfferButton.interactable = !localGame;
        StartCoroutine(ShowInGameMenuNextFrame());
    }
    private void OnMakeMoveClient(NetMessage msg)
    {
        NetMakeMove mm = msg as NetMakeMove;
        Debug.Log($"[CLIENT] Received move team={mm.team} orig=({mm.origX},{mm.origY}) dest=({mm.destX},{mm.destY}) myTeam={currentTeam}");
        if(mm.team != currentTeam)
        {
            Piece target = Pieces[mm.origX, mm.origY];
            avMoves = target.GetAvailavleMoves(ref Pieces, BORD_COUNT_X, BORD_COUNT_Y);
            specialMove = target.GetSpecialMove(ref Pieces, ref moveList, ref avMoves);
            MoveTo(mm.origX, mm.origY, mm.destX, mm.destY);
        }
        else
        {
            Debug.Log($"[CLIENT] Move IGNORED because mm.team == currentTeam == {currentTeam}");
        }
    }
    private void OnRematchServer(NetMessage msg, NetworkConnection cnn)
    {
        Server.Instance.Broadcast(msg);
    }
    private void OnRematchClient(NetMessage msg)
    {
       NetRematch rm = msg as NetRematch;
        playerRematch[rm.team] = rm.wantRematch == 1;
        if(rm.team != currentTeam)
        {
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            if (rm.wantRematch !=1)
            {
                rematchButton.interactable = false;
            }
        }
        if (playerRematch[0] && playerRematch[1])
        {
            GameReset();
            // Online παιχνίδι
            StartCoroutine(ShowInGameMenuNextFrame());
        }
    }
    private void OnConnectionDropped()
    {
        if (localGame || onlineDisconnected) return;
        onlineDisconnected = true;

        // Αποτυχία σύνδεσης 
        if (currentTeam == -1)
        {
            GameUI.Instance.ShowConnectionError("Room is no longer available.");
            return;
        }

        HideInGameMenu();
        currentlyDragging = null;
        HideAvMoves();
        WinnerScene.SetActive(true);
        rematchButton.interactable = false;
        for (int i = 0; i < rematchIndicator.childCount; i++)
            rematchIndicator.GetChild(i).gameObject.SetActive(false);
        if (opponentLeftPanel != null)
            opponentLeftPanel.SetActive(true);
    }
    public void OnDrawButton()
    {
        if (localGame || drawOfferedByUs || drawOffersCount >= 3) return;
        drawOffersCount++;
        drawOfferedByUs = true;
        drawOfferButton.interactable = false;
        drawOfferBtnText.text = "DRAW OFFERED...";
        NetDraw nd = new NetDraw();
        nd.wantDraw = 1;
        Client.Instance.SendToServer(nd);
    }

    private void OnAcceptDraw()
    {
        drawRespondPanel.SetActive(false);
        NetDraw nd = new NetDraw();
        nd.wantDraw = 2;
        Client.Instance.SendToServer(nd);
        DisplayDraw(); // εμφάνιση οθόνης ισοπαλίας 
    }

    private void OnDeclineDraw()
    {
        drawRespondPanel.SetActive(false);
        NetDraw nd = new NetDraw();
        nd.wantDraw = 0;
        Client.Instance.SendToServer(nd);
    }

    private System.Collections.IEnumerator ShowInGameMenuNextFrame()
    {
        yield return null;
        ShowInGameMenu();
    }

    private System.Collections.IEnumerator ResetDrawButtonAfterDelay()
    {
        yield return new WaitForSeconds(15f);
        if (drawOffersCount >= 3)
        {
            if (drawOfferBtnText != null) drawOfferBtnText.text = "DRAW LIMIT";
        }
        else
        {
            if (drawOfferBtnText != null) drawOfferBtnText.text = "OFFER DRAW";
            if (drawOfferButton != null)  drawOfferButton.interactable = true;
        }
    }

    private void OnDrawServer(NetMessage msg, NetworkConnection cnn)
    {
        Server.Instance.BroadcastExcept(msg, cnn);
    }

    private void OnDrawClient(NetMessage msg)
    {
        NetDraw nd = msg as NetDraw;
        if (nd.wantDraw == 1)
        {
            // προτείνει ισοπαλία
            drawRespondPanel.SetActive(true);
        }
        else if (nd.wantDraw == 0)
        {
            // Ισοπαλία απορρίφθηκε 
            drawOfferedByUs = false;
            drawOfferBtnText.text = "DRAW DECLINED";
            StopCoroutine("ResetDrawButtonAfterDelay");
            StartCoroutine("ResetDrawButtonAfterDelay");
        }
        else if (nd.wantDraw == 2)
        {
            // Ισοπαλία αποδεκτή 
            drawRespondPanel.SetActive(false);
            DisplayDraw();
        }
    }

    private void OnSetLocalGame(bool flag)
    {
        playerCount = -1;
        currentTeam = -1;
        localGame = flag;
        onlineDisconnected = false;
    }
    private void BackToMenuDelay()
    {
        Client.Instance.ShutDown();
        Server.Instance.ShutDown();
    }
}

