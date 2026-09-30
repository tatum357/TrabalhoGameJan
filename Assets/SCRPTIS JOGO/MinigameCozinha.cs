using UnityEngine;
using UnityEngine.UI;

// Gerencia o pop-up do minigame no canvas (abrir/fechar) e a logica
// do jogo da barra de timing:
//   - um cursor anda numa barra horizontal (ida e volta), sempre
//     INTEIRO dentro da barra preta (nunca passa da borda);
//   - existe um ponto perfeito (zona verde) sorteado em cada rodada,
//     sempre COMPLETO e visivel dentro da barra preta (com folga);
//   - o jogador para o cursor com E (ou clique na barra / Espaco);
//   - quanto mais longe do ponto perfeito, pior a nota:
//       PERFEITO -> BOM -> MAIS OU MENOS -> RUIM.
// A UI e criada em runtime dentro de "Area Jogo" (nao precisa mexer na
// cena; o texto antigo "AQUI ENTRA O MINI-GAME" e escondido sozinho).
// A barra preta e o "chao" estatico; a zona verde e o cursor amarelo
// sao objetos SEPARADOS da barra (irmaos, nao filhos) - so eles se
// mexem, a barra preta nunca se move, e nada sai da area do painel.
// Quem decide quando interagir e o ControladorInteracao (tecla E perto
// do objeto). Anexado ao "Canvas _ Sistema".
public class MinigameCozinha : MonoBehaviour
{
    [Header("UI do Canvas (preenchida)")]
    public GameObject painelMinigame; // pop-up do minigame
    public Button btnFechar;

    // Para o menu (Esc) e o ControladorInteracao saberem o estado
    public static bool JogoAberto { get; private set; }
    public static MinigameCozinha Instancia { get; private set; }

    // Nota da ultima jogada (para outros sistemas usarem no futuro):
    // -1 = nada jogado ainda; caso contrario 0..1 (1 = perfeito).
    public static float UltimaQualidade { get; private set; } = -1f;
    public static string UltimaNota { get; private set; } = "";

    // --- Regras da barra (distancias sao fracoes do comprimento da barra) ---
    private const float LimPerfeito = 0.045f;  // ate aqui = PERFEITO (zona verde)
    private const float LimBom = 0.10f;        // ate aqui = BOM
    private const float LimMedia = 0.18f;      // ate aqui = MAIS OU MENOS; longe = RUIM
    private const float LarguraBarra = 440f;   // pixels
    private const float AlturaBarra = 40f;
    private const float LarguraCursor = 8f;    // pixels
    private const float YBarra = 10f;          // centro da barra preta em Area Jogo

    // O cursor so anda de modo a ficar INTEIRO dentro da barra preta:
    // os limites (em fracao da barra) sao meia largura do cursor nas pontas.
    private const float TMinCursor = (LarguraCursor * 0.5f) / LarguraBarra;
    private const float TMaxCursor = 1f - TMinCursor;

    private int frameAbertura = -1;

    // --- estado da barra ---
    private enum Estado { Parado, Rodando, Resultado }
    private Estado estado = Estado.Parado;

    private bool uiCriada;
    private RectTransform barraRT;
    private RectTransform zonaRT;
    private RectTransform cursorRT;
    private Text txtInstrucao;
    private Text txtNota;

    private float cursorT;    // 0..1 posicao atual do cursor
    private float alvoT;      // 0..1 centro do ponto perfeito (zona verde)
    private float velocidade; // comprimentos de barra por segundo
    private float direcao = 1f;
    private float tempoResultado = -1f;

    private void Awake()
    {
        Instancia = this;
        JogoAberto = false;

        if (painelMinigame != null) painelMinigame.SetActive(false);
        if (btnFechar != null) btnFechar.onClick.AddListener(Fechar);

        GarantirUI(); // cria a barra uma unica vez (a gente pode fechar/abrir a vontade)
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private void LateUpdate()
    {
        // LateUpdate roda DEPOIS de todos os Updates: o minigame "come" a tecla
        // antes que o MenuSistema tente abrir o menu com o mesmo Esc.
        if (!JogoAberto) return;

        // Nao processa no mesmo frame em que abriu (a mesma tecla E que abriu)
        if (Time.frameCount == frameAbertura) return;

        if (estado == Estado.Rodando)
        {
            MoverCursor();

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                PararCursor();
            else if (Input.GetKeyDown(KeyCode.Escape))
                Fechar();
        }
        else if (estado == Estado.Resultado)
        {
            // Esc fecha direto; E so fecha depois de um pequeno atraso, para
            // nao fechar no duplo toque da tecla que acabou de parar a barra.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Fechar();
            }
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            {
                if (Time.time - tempoResultado > 0.45f)
                    Fechar();
            }
        }
    }

    public void Abrir()
    {
        if (JogoAberto || MenuSistema.MenuAberto) return;
        JogoAberto = true;
        frameAbertura = Time.frameCount;
        if (painelMinigame != null) painelMinigame.SetActive(true);
        GarantirUI();
        NovaRodada();
    }

    public void Fechar()
    {
        if (!JogoAberto) return;
        JogoAberto = false;
        estado = Estado.Parado;
        if (painelMinigame != null) painelMinigame.SetActive(false);
    }

    // ---------------- logica da barra ----------------

    private void NovaRodada()
    {
        // Se a UI nao foi criada (referencia quebrada), nao quebra o jogo:
        // fica no estado Parado e o painel so abre/fecha normal.
        if (!uiCriada) return;

        // Ponto perfeito: a zona verde (meio a meio = +/- LimPerfeito da
        // barra) so e sorteada COMPLETA dentro da barra preta, com folga
        // extra nas pontas - o jogador sempre ve o ponto inteiro.
        float folgaZona = LimPerfeito + 0.10f;
        alvoT = UnityEngine.Random.Range(folgaZona, 1f - folgaZona);
        // velocidade sorteada tambem para a rodada nao ser sempre igual
        velocidade = UnityEngine.Random.Range(0.75f, 1f);
        // cursor comeca na ponta esquerda, ja inteiro dentro da barra
        cursorT = TMinCursor;
        direcao = 1f;
        estado = Estado.Rodando;
        tempoResultado = -1f;
        UltimaQualidade = -1f;
        UltimaNota = "";

        zonaRT.anchoredPosition = new Vector2(-LarguraBarra * 0.5f + alvoT * LarguraBarra, YBarra);
        AtualizarCursor();

        txtInstrucao.text = "Aperte E quando o cursor estiver no verde!";
        txtInstrucao.color = new Color(0.16f, 0.13f, 0.1f);
        txtNota.text = "Aperte E (ou clique) na hora certa!";
        txtNota.color = new Color(0.28f, 0.3f, 0.27f);
    }

    private void MoverCursor()
    {
        // Vai e volta SEMPRE inteiro dentro da barra preta: quando chega
        // na folga da borda (TMin/TMax), a metade de fora do cursor nao
        // ultrapassa o preto - ele só vira.
        cursorT += direcao * velocidade * Time.deltaTime;
        if (cursorT >= TMaxCursor)
        {
            cursorT = TMaxCursor;
            direcao = -1f;
        }
        else if (cursorT <= TMinCursor)
        {
            cursorT = TMinCursor;
            direcao = 1f;
        }
        AtualizarCursor();
    }

    private void AtualizarCursor()
    {
        if (cursorRT == null) return;
        // Centro da barra preta = (-LarguraBarra/2, YBarra) em Area Jogo;
        // cursorT 0..1 mapeia da borda esquerda p/ direita da barra preta.
        cursorRT.anchoredPosition = new Vector2(-LarguraBarra * 0.5f + cursorT * LarguraBarra, YBarra);
    }

    private void PararCursor()
    {
        float dist = Mathf.Abs(cursorT - alvoT);

        string nota;
        Color cor;
        if (dist <= LimPerfeito)
        {
            nota = "PERFEITO!";
            cor = new Color(0.05f, 0.5f, 0.12f);
        }
        else if (dist <= LimBom)
        {
            nota = "BOM";
            cor = new Color(0.08f, 0.35f, 0.65f);
        }
        else if (dist <= LimMedia)
        {
            nota = "MAIS OU MENOS";
            cor = new Color(0.72f, 0.42f, 0.03f);
        }
        else
        {
            nota = "RUIM";
            cor = new Color(0.7f, 0.1f, 0.08f);
        }

        UltimaNota = nota;
        UltimaQualidade = Mathf.Clamp01(1f - dist / LimMedia);

        float pct = dist * 100f;
        txtNota.color = cor;
        txtNota.text = nota + "\nDistancia do ponto: " + pct.ToString("F1") + "%";
        txtInstrucao.text = "Pressione E para fechar";
        txtInstrucao.color = new Color(0.3f, 0.3f, 0.3f);

        // cursor congela onde parou (da para ver se passou do verde)
        estado = Estado.Resultado;
        tempoResultado = Time.time;
    }

    private void ClicouNaBarra()
    {
        if (JogoAberto && estado == Estado.Rodando)
            PararCursor();
    }

    // ---------------- criacao da UI (uma vez so) ----------------

    private void GarantirUI()
    {
        if (uiCriada || painelMinigame == null) return;

        Transform area = painelMinigame.transform.Find("Caixa Minigame/Area Jogo");
        if (area == null) area = painelMinigame.transform; // fallback: nao quebra o jogo
        int camada = area.gameObject.layer;

        // O texto de placeholder da cena sai; a barra substitui ele.
        Transform antigo = area.Find("Txt Area");
        if (antigo != null) antigo.gameObject.SetActive(false);

        Font fonte = PegarFonte();

        // Instrucao (topo da area)
        txtInstrucao = CriarTexto(area, "Txt Instrucao", camada, fonte, 18, FontStyle.Bold);
        txtInstrucao.rectTransform.anchorMin = new Vector2(0f, 1f);
        txtInstrucao.rectTransform.anchorMax = new Vector2(1f, 1f);
        txtInstrucao.rectTransform.pivot = new Vector2(0.5f, 1f);
        txtInstrucao.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        txtInstrucao.rectTransform.sizeDelta = new Vector2(0f, 40f);
        txtInstrucao.color = new Color(0.16f, 0.13f, 0.1f);

        // Fundo da barra (meio da area). Tambem aceita clique para parar.
        GameObject barraGo = new GameObject("Barra Fundo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barraGo.layer = camada;
        barraGo.transform.SetParent(area, false);
        barraRT = barraGo.GetComponent<RectTransform>();
        barraRT.anchorMin = new Vector2(0.5f, 0.5f);
        barraRT.anchorMax = new Vector2(0.5f, 0.5f);
        barraRT.pivot = new Vector2(0.5f, 0.5f);
        barraRT.anchoredPosition = new Vector2(0f, YBarra);
        barraRT.sizeDelta = new Vector2(LarguraBarra, AlturaBarra);
        Image imgBarra = barraGo.GetComponent<Image>();
        imgBarra.color = new Color(0.16f, 0.16f, 0.19f);

        // Zona verde do ponto perfeito: SEPARADA da barra preta (irma da
        // barra, nao filha) - so a zona se mexe, a barra preta fica parada.
        // Largura = faixa PERFEITO; nasce depois da barra na ordem dos
        // irmaos, entao renderiza por cima do preto.
        zonaRT = CriarRetangulo(area, "Zona Alvo", camada, new Color(0.25f, 0.8f, 0.35f, 0.95f));
        zonaRT.sizeDelta = new Vector2(2f * LimPerfeito * LarguraBarra, AlturaBarra);

        // Cursor amarelo: tambem IRMA da barra preta (separado), por cima
        // da zona; mesma altura da barra para ficar INTEIRO dentro do preto
        cursorRT = CriarRetangulo(area, "Cursor", camada, new Color(1f, 0.92f, 0.2f));
        cursorRT.sizeDelta = new Vector2(LarguraCursor, AlturaBarra);

        // Clique na barra para (zona/cursor tem raycast desligado, entao o
        // clique sempre chega no fundo da barra)
        Button btnBarra = barraGo.AddComponent<Button>();
        btnBarra.transition = Selectable.Transition.None;
        btnBarra.targetGraphic = imgBarra;
        btnBarra.onClick.AddListener(ClicouNaBarra);

        // Nota (embaixo da area)
        txtNota = CriarTexto(area, "Txt Nota", camada, fonte, 22, FontStyle.Bold);
        txtNota.rectTransform.anchorMin = new Vector2(0f, 0f);
        txtNota.rectTransform.anchorMax = new Vector2(1f, 0f);
        txtNota.rectTransform.pivot = new Vector2(0.5f, 0f);
        txtNota.rectTransform.anchoredPosition = new Vector2(0f, 25f);
        txtNota.rectTransform.sizeDelta = new Vector2(0f, 75f);
        txtNota.color = new Color(0.28f, 0.3f, 0.27f);

        uiCriada = true;
    }

    private Text CriarTexto(Transform pai, string nome, int camada, Font fonte, int tamanho, FontStyle estilo)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.layer = camada;
        go.transform.SetParent(pai, false);
        Text t = go.GetComponent<Text>();
        t.font = fonte;
        t.fontSize = tamanho;
        t.fontStyle = estilo;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false; // nao pode bloquear o clique na barra
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    private RectTransform CriarRetangulo(Transform pai, string nome, int camada, Color cor)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = camada;
        go.transform.SetParent(pai, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        // Ancorada no CENTRO de "Area Jogo" (mesma referencia da barra
        // preta): posicao = centro da barra + fracao do comprimento.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, YBarra);
        Image img = go.GetComponent<Image>();
        img.color = cor;
        img.raycastTarget = false; // deixa o clique passar pro fundo da barra
        return rt;
    }

    private Font PegarFonte()
    {
        // Reusa a fonte do titulo da cena (mesma fonte do resto da UI)
        Transform caixa = painelMinigame.transform.Find("Caixa Minigame");
        if (caixa != null)
        {
            Transform titulo = caixa.Find("Titulo Minigame");
            if (titulo != null)
            {
                Text t = titulo.GetComponent<Text>();
                if (t != null && t.font != null) return t.font;
            }
        }

        // Fallback: fonte embutida do Unity
        try
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}
