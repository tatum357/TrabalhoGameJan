using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

// ===============================================================
//  CONTROLE DA COZINHA - recebe os pedidos dos clientes
//
//  Fluxo:
//    1. o NPC com a tag "cliente" sorteia um numero (padrao 1 a 10)
//       e chama ReceberPedido(numero);
//    2. aqui o numero e comparado com o numero de cada prato;
//    3. achou o prato -> aparece um icone no canto SUPERIOR DIREITO
//       da tela avisando que chegou um pedido;
//    4. encostar (ou clicar) no icone mostra o detalhe: qual e o
//       pedido e os ingredientes pra fazer ele.
//
//  No Inspector:
//    - "Pratos": numero do pedido + nome + ingredientes (lista vazia
//      cria 10 pratos padrao, numerados de 1 a 10);
//    - "Cadeiras": quantidade de GRUPOS = tamanho do array; cada
//      posicao e UM grupo (arraste o GameObject do grupo). Dentro
//      de cada grupo os FILHOS sao os pontos de sentar: o cliente
//      sorteia o grupo e depois UM ponto dentro dele.
//
//  A UI dos icones e criada em runtime (canvas proprio), entao nao
//  precisa mexer na cena pra ela funcionar.
// ===============================================================
public class ControleCozinha : MonoBehaviour
{
    [Serializable]
    public class Prato
    {
        [Tooltip("Numero do pedido (1 a 10 por padrao). O pedido so aparece se o numero bater com o sorteio do cliente.")]
        public int numero = 1;
        [Tooltip("Nome do pedido exibido no detalhe")]
        public string nome = "Prato";
        [Tooltip("Ingredientes exibidos no detalhe do pedido")]
        public string[] ingredientes = new string[0];
    }

    [Header("Pratos (numeracao dos pedidos)")]
    [Tooltip("O pedido do cliente so mostra o icone se o numero bater com o 'numero' de algum prato. Lista vazia = 10 pratos padrao (1 a 10).")]
    public Prato[] pratos;

    [Header("Cadeiras (grupos com pontos)")]
    [FormerlySerializedAs("mesas")]
    [Tooltip("Grupos de cadeiras = tamanho do array. Cada posicao = UM grupo (arraste o GameObject do grupo). Dentro de cada grupo os FILHOS sao os pontos de sentar: o cliente sorteia o grupo e depois UM ponto dentro dele.")]
    public Transform[] cadeiras;

    [Header("Bloqueio de cadeira ocupada")]
    [Tooltip("Tamanho (em metros) do bloqueio NavMesh que liga quando um NPC senta - a cadeira fica seletiva: so ele pode usar ate sair.")]
    public float bloqueioCadeira = 0.6f;

    // Singleton simples: o resto do jogo acessa por Instancia/Obter
    public static ControleCozinha Instancia { get; private set; }

    // --- estado da UI (tudo criado em runtime) ---
    private RectTransform conteinerIcones;
    private RectTransform painelDetalhe;
    private Text txtDetalhe;
    private readonly List<Prato> pedidos = new List<Prato>(); // pedido de cada icone
    private int detalheAberto = -1;  // icone com detalhe na tela
    private int detalheFixado = -1;  // icone fixado com clique
    private bool uiPronta;

    private void Awake()
    {
        Instancia = this;
        GarantirPratosPadrao();
        GarantirUI();
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    // Acha o controle da cena; se nao existir, cria um na hora (o jogo
    // nunca quebra por falta dele).
    public static ControleCozinha Obter()
    {
        if (Instancia != null) return Instancia;

        ControleCozinha c = FindAnyObjectByType<ControleCozinha>();
        if (c == null)
        {
            GameObject go = new GameObject("Controle Cozinha");
            c = go.AddComponent<ControleCozinha>(); // o Awake prepara tudo
        }
        return c;
    }

    // ===============================================================
    //  PEDIDOS (chamado pelo NPC cliente)
    // ===============================================================

    // Recebe o numero sorteado pelo cliente e compara com o numero de
    // cada prato. Achou = mostra o icone do pedido no canto da tela.
    public void ReceberPedido(int numero)
    {
        GarantirPratosPadrao();

        Prato achado = BuscarPrato(numero);
        if (achado == null)
        {
            Debug.LogWarning("[Cozinha] Pedido " + numero + " nao tem prato com esse numero. Confira a lista 'Pratos' no Inspector do Controle Cozinha.", this);
            return;
        }

        CriarIcone(achado);
        Debug.Log("[Cozinha] Pedido " + numero + " ('" + achado.nome + "') recebido - icone no canto da tela.");
    }

    // O prato com o numero pedido (null se nenhum tiver esse numero)
    public Prato BuscarPrato(int numero)
    {
        if (pratos == null) return null;
        for (int i = 0; i < pratos.Length; i++)
        {
            if (pratos[i] != null && pratos[i].numero == numero)
                return pratos[i];
        }
        return null;
    }

    // Sorteia uma cadeira LIVRE: passa pelos grupos a partir de uma
    // posicao aleatoria e, no grupo, sorteia entre os pontos livres
    // (pula cadeira ocupada, filho desligado e posicao vazia).
    // null = nenhuma cadeira livre (todas ocupadas).
    public Transform SortearCadeira()
    {
        if (cadeiras == null || cadeiras.Length == 0) return null;

        int grupoIni = UnityEngine.Random.Range(0, cadeiras.Length);
        for (int g = 0; g < cadeiras.Length; g++)
        {
            Transform grupo = cadeiras[(grupoIni + g) % cadeiras.Length];
            if (grupo == null) continue;

            // grupo sem filhos: o proprio grupo serve de ponto
            if (grupo.childCount == 0)
            {
                if (EstaLivre(grupo)) return grupo;
                continue;
            }

            // procura um ponto livre dentro do grupo
            int quantos = grupo.childCount;
            int pontoIni = UnityEngine.Random.Range(0, quantos);
            for (int i = 0; i < quantos; i++)
            {
                Transform ponto = grupo.GetChild((pontoIni + i) % quantos);
                if (!ponto.gameObject.activeSelf) continue; // desligado
                if (EstaLivre(ponto)) return ponto;
            }
            // grupo todo ocupado: tenta o proximo
        }
        return null;
    }

    // ===============================================================
    //  CADEIRA OCUPADA - "nav mech seletivo" por ponto
    //
    //  Quando o NPC senta, Reservar() marca a cadeira e LIGA um
    //  NavMeshObstacle nela: nenhum outro NPC entra/senta ate
    //  Liberar() (quando ele levanta e sai).
    // ===============================================================

    private readonly HashSet<Transform> cadeirasOcupadas = new HashSet<Transform>();

    // true = a cadeira ainda nao tem ninguem sentado
    public bool EstaLivre(Transform ponto)
    {
        return ponto != null && !cadeirasOcupadas.Contains(ponto);
    }

    // NPC sentou: ocupa a cadeira e liga o bloqueio nela
    public void Reservar(Transform ponto)
    {
        if (ponto == null || cadeirasOcupadas.Contains(ponto)) return;

        cadeirasOcupadas.Add(ponto);
        AtivarBloqueio(ponto, true);
        Debug.Log("[Cozinha] Cadeira ocupada (" + cadeirasOcupadas.Count + " no momento) - outros NPCs ficam fora.", ponto);
    }

    // NPC saiu: libera a cadeira e desliga o bloqueio
    public void Liberar(Transform ponto)
    {
        if (ponto == null || !cadeirasOcupadas.Remove(ponto)) return;

        AtivarBloqueio(ponto, false);
        Debug.Log("[Cozinha] Cadeira liberada - pode ser ocupada de novo.", ponto);
    }

    // Liga/desliga o obstaculo NavMesh do ponto (criado uma vez so)
    private void AtivarBloqueio(Transform ponto, bool ligar)
    {
        NavMeshObstacle obs = ponto.GetComponent<NavMeshObstacle>();
        if (obs == null && ligar)
        {
            obs = ponto.gameObject.AddComponent<NavMeshObstacle>();
            obs.carving = true;   // versao 2022.3 chama 'carving' (nao 'carve')
        }
        if (obs == null) return;

        obs.center = Vector3.zero;
        obs.size = new Vector3(bloqueioCadeira, bloqueioCadeira, 2f);
        obs.enabled = ligar;
    }

    // ===============================================================
    //  DETALHE DO PEDIDO (aparece quando o jogador encosta no icone)
    // ===============================================================

    // Passou o mouse em cima do icone "indice"
    public void MostrarDetalhe(int indice)
    {
        AbrirDetalhe(indice);
    }

    // Tirou o mouse de cima (so fecha se nao estiver fixado com clique)
    public void EsconderDetalhe(int indice)
    {
        if (detalheFixado != indice) FecharDetalhe();
    }

    // Clique no icone: fixa o detalhe; clicar de novo, esconde
    public void AlternarDetalhe(int indice)
    {
        if (detalheFixado == indice)
        {
            detalheFixado = -1;
            FecharDetalhe();
        }
        else
        {
            detalheFixado = indice;
            AbrirDetalhe(indice);
        }
    }

    private void AbrirDetalhe(int indice)
    {
        if (!uiPronta || indice < 0 || indice >= pedidos.Count) return;
        if (detalheAberto == indice && painelDetalhe.gameObject.activeSelf) return;

        Prato prato = pedidos[indice];
        txtDetalhe.text =
            "Pedido: " + prato.nome + "\n" +
            "Numero: " + prato.numero + "\n\n" +
            "Ingredientes:" + ListarIngredientes(prato);

        // alinha o painel na mesma altura do icone escolhido
        painelDetalhe.anchoredPosition = new Vector2(-84f, -12f - indice * 64f);
        painelDetalhe.gameObject.SetActive(true);
        detalheAberto = indice;
    }

    private void FecharDetalhe()
    {
        detalheAberto = -1;
        if (painelDetalhe != null) painelDetalhe.gameObject.SetActive(false);
    }

    // "- ingrediente" por linha, a partir da lista do prato
    private string ListarIngredientes(Prato prato)
    {
        if (prato.ingredientes == null || prato.ingredientes.Length == 0)
            return "\n- (sem ingredientes cadastrados)";

        string lista = "";
        for (int i = 0; i < prato.ingredientes.Length; i++)
        {
            if (string.IsNullOrEmpty(prato.ingredientes[i])) continue;
            lista += "\n- " + prato.ingredientes[i];
        }
        return (lista == "") ? "\n- (sem ingredientes cadastrados)" : lista;
    }

    // ===============================================================
    //  UI - criada uma vez em runtime (canvas proprio, nada de cena)
    // ===============================================================
    private void GarantirUI()
    {
        if (uiPronta) return;
        uiPronta = true;

        // Canvas proprio, por cima das outras telas, com raycaster
        // (o EventSystem da cena e quem trata o mouse)
        GameObject canvasGo = new GameObject("Canvas Pedidos",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = 5; // UI
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // fica visivel por cima do resto

        // Conteiner no canto SUPERIOR DIREITO (sem imagem: nao bloqueia clique)
        GameObject cont = new GameObject("Icones", typeof(RectTransform));
        cont.layer = 5;
        cont.transform.SetParent(canvasGo.transform, false);
        conteinerIcones = cont.GetComponent<RectTransform>();
        conteinerIcones.anchorMin = new Vector2(1f, 1f);
        conteinerIcones.anchorMax = new Vector2(1f, 1f);
        conteinerIcones.pivot = new Vector2(1f, 1f);
        conteinerIcones.anchoredPosition = new Vector2(-12f, -12f);
        conteinerIcones.sizeDelta = new Vector2(64f, 400f);

        // Painel do detalhe (escondido ate encostar no icone)
        GameObject det = new GameObject("Detalhe Pedido",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        det.layer = 5;
        det.transform.SetParent(canvasGo.transform, false);
        painelDetalhe = det.GetComponent<RectTransform>();
        painelDetalhe.anchorMin = new Vector2(1f, 1f);
        painelDetalhe.anchorMax = new Vector2(1f, 1f);
        painelDetalhe.pivot = new Vector2(1f, 1f);
        painelDetalhe.anchoredPosition = new Vector2(-84f, -12f);
        painelDetalhe.sizeDelta = new Vector2(250f, 180f);
        Image imgDet = det.GetComponent<Image>();
        imgDet.color = new Color(0.1f, 0.1f, 0.13f, 0.97f);
        imgDet.raycastTarget = false; // o mouse continua "passando por ele"

        // Texto do detalhe (nome + numero + ingredientes)
        GameObject txtGo = new GameObject("Txt Detalhe",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.layer = 5;
        txtGo.transform.SetParent(det.transform, false);
        txtDetalhe = txtGo.GetComponent<Text>();
        txtDetalhe.font = PegarFonte();
        txtDetalhe.fontSize = 15;
        txtDetalhe.alignment = TextAnchor.UpperLeft;
        txtDetalhe.color = Color.white;
        txtDetalhe.raycastTarget = false;
        txtDetalhe.horizontalOverflow = HorizontalWrapMode.Wrap;
        txtDetalhe.verticalOverflow = VerticalWrapMode.Overflow;
        AjustarPreencher(txtDetalhe.rectTransform, 10f);

        det.SetActive(false);
    }

    // Cria um icone (quadrado com o numero do pedido) no canto
    private void CriarIcone(Prato prato)
    {
        GarantirUI();
        if (conteinerIcones == null) return;

        int indice = pedidos.Count;
        pedidos.Add(prato);

        GameObject go = new GameObject("Icone Pedido " + prato.numero,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(conteinerIcones, false);

        // empilhados de cima pra baixo no canto direito
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -indice * 64f);
        rt.sizeDelta = new Vector2(56f, 56f);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.85f, 0.3f, 0.14f); // badge chamativo
        img.raycastTarget = true; // precisa receber o mouse em cima

        // numero do pedido em cima do quadrado
        GameObject txtGo = new GameObject("Txt Numero",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.layer = 5;
        txtGo.transform.SetParent(go.transform, false);
        Text txt = txtGo.GetComponent<Text>();
        txt.font = PegarFonte();
        txt.fontSize = 26;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = prato.numero.ToString();
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        AjustarPreencher(txt.rectTransform, 0f);

        // Encostar = mostra o detalhe; clicar = fixa/esconde
        PedidoIcone icone = go.AddComponent<PedidoIcone>();
        icone.controle = this;
        icone.indice = indice;
    }

    // Estica um RectTransform pra ocupar o pai todo com uma folga
    private void AjustarPreencher(RectTransform rt, float folga)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(folga, folga);
        rt.offsetMax = new Vector2(-folga, -folga);
    }

    // Se o Inspector nao tiver pratos, cria 10 padroes (1 a 10) pra o
    // jogo funcionar direto - e so editar a lista la no Inspector.
    // (publico: o script de reinicio da cena chama pra ja deixar os
    // 10 pratos visiveis no Inspector)
    public void GarantirPratosPadrao()
    {
        if (pratos != null && pratos.Length > 0) return;

        string[] nomes =
        {
            "Pizza", "Hamburguer", "Salada", "Macarrao", "Arroz com Frango",
            "Sopa", "Sanduiche", "Omelete", "Strogonoff", "Sobremesa"
        };
        string[][] receitas =
        {
            new string[] { "massa", "molho de tomate", "queijo" },
            new string[] { "pao", "carne", "alface", "tomate" },
            new string[] { "alface", "tomate", "queijo", "azeite" },
            new string[] { "macarrao", "molho de tomate", "queijo" },
            new string[] { "arroz", "frango", "temperos" },
            new string[] { "caldo", "legumes", "sal" },
            new string[] { "pao", "presunto", "queijo", "manteiga" },
            new string[] { "ovos", "queijo", "sal" },
            new string[] { "frango", "creme de leite", "molho branco" },
            new string[] { "leite", "acucar", "canela" }
        };

        pratos = new Prato[nomes.Length];
        for (int i = 0; i < pratos.Length; i++)
        {
            pratos[i] = new Prato();
            pratos[i].numero = i + 1;             // 1 a 10
            pratos[i].nome = nomes[i];
            pratos[i].ingredientes = receitas[i];
        }
    }

    // Reusa a fonte de qualquer texto da cena; se nao achar, a embutida
    private Font PegarFonte()
    {
        Text qualquer = FindAnyObjectByType<Text>(FindObjectsInactive.Include);
        if (qualquer != null && qualquer.font != null) return qualquer.font;

        try
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (Exception)
        {
            return null;
        }
    }
}

// ===============================================================
//  ICONE DO PEDIDO - o mouse em cima do icone:
//    encostar ........ mostra o detalhe do pedido
//    tirar o mouse ... esconde (se nao tiver fixado com clique)
//    clicar .......... fixa o detalhe (clique de novo = esconde)
//  Componente adicionado em runtime pelo ControleCozinha.
// ===============================================================
public class PedidoIcone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ControleCozinha controle; // dono do pedido
    public int indice;               // qual pedido deste icone

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (controle != null) controle.MostrarDetalhe(indice);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (controle != null) controle.EsconderDetalhe(indice);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controle != null) controle.AlternarDetalhe(indice);
    }
}
