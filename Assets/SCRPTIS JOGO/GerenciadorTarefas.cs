using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// TELA DE GERENCIAMENTO DE NPCs
// O player abre a tela, toca num local do "mapinha da cozinha" (cada marca
// equivale a um objeto interagivel do cenario) e escolhe um NPC da equipe
// para mandar para aquela tarefa:
//
//   1. SelecionarPonto(i)  - toque no local do mapa
//   2. EnviarNPC(npc)      - escolha do NPC -> npc.MandarPara(local)
//   3. quando o NPC chega  -> controlador.InteragirNPC(...) -> fazerTarefas
//                             -> resultado em npc.DeuCerto / DeuCertoNPC
//
// Anexar em um objeto da cena (vazio ou no Canvas). A tela (painel + mapa +
// botoes) e montada no Editor e ligada nos campos la embaixo.
public class GerenciadorTarefas : MonoBehaviour
{
    [System.Serializable]
    public class PontoDoMapa
    {
        [Tooltip("Nome exibido no log (ex.: Grelha)")]
        public string nome;
        [Tooltip("Objeto interagivel do cenario (com a tag objetoIteragivel)")]
        public GameObject objeto;
        [Tooltip("Marca clicavel no mapa (botao dentro da area do mapa)")]
        public Button botao;
        [Tooltip("O que o NPC vai fazer la: um dos cases do fazerTarefas (ex.: grelha)")]
        public string statusTarefa;
    }

    [Header("Painel da tela")]
    public GameObject painelGerenciamento;
    [Tooltip("Pausa o jogo com a tela aberta (o NPC so anda depois que fechar)")]
    public bool pausarJogo = true;

    [Header("Mapa da cozinha")]
    [Tooltip("Cada ponto = uma marca clicavel no mapa + o objeto do cenario")]
    public PontoDoMapa[] pontos;

    [Header("Escolha do NPC")]
    [Tooltip("Painel que aparece depois que o local e escolhido (opcional)")]
    public GameObject painelEscolherNPC;
    [Tooltip("Modelo de botao (fica inativo); um e criado por NPC da equipe")]
    public Button modeloBotaoNPC;
    public Transform conteinerNPCs;
    [Tooltip("NPCs da equipe (vazio = pega todos da cena)")]
    public garson[] equipe;

    [Header("Referencias")]
    [Tooltip("Deixe vazio para achar automaticamente")]
    public ControladorInteracao controlador;
    [Tooltip("Botao FECHAR da tela (ligado sozinho no Start)")]
    public Button botaoFechar;

    [Header("Depois da tarefa")]
    [Tooltip("Sem ordem o NPC fica em idle; marcado, ele ja volta pro idle ao terminar")]
    public bool voltarParaIdleAposTarefa = true;

    // Para o resto do jogo saber que a tela esta aberta (menu/controlador)
    public static bool Aberto { get; private set; }

    private int pontoSelecionado = -1;
    private float timeScaleAntes = 1f;
    private bool botoesCriados;

    // Pedidos em andamento: NPC a caminho de um ponto
    private class Pedido
    {
        public garson npc;
        public PontoDoMapa ponto;
    }
    private readonly List<Pedido> pedidos = new List<Pedido>();

    private void Awake()
    {
        Aberto = false;
        if (painelGerenciamento != null) painelGerenciamento.SetActive(false);
        if (painelEscolherNPC != null) painelEscolherNPC.SetActive(false);
    }

    private void Start()
    {
        if (controlador == null)
            controlador = FindAnyObjectByType<ControladorInteracao>();

        // Liga o clique de cada marca do mapa
        for (int i = 0; i < pontos.Length; i++)
        {
            if (pontos[i].botao == null) continue;
            int indice = i; // captura o indice pro lambda
            pontos[i].botao.onClick.AddListener(() => SelecionarPonto(indice));
        }

        if (botaoFechar != null)
            botaoFechar.onClick.AddListener(Fechar);

        CriarBotoesNPC();
    }

    // Cria um botao por NPC da equipe (a partir do modelo)
    private void CriarBotoesNPC()
    {
        if (botoesCriados) return;   // ja criados (evita duplicar)
        if (modeloBotaoNPC == null || conteinerNPCs == null) return;

        if (equipe == null || equipe.Length == 0)
            equipe = FindObjectsByType<garson>(FindObjectsSortMode.None);

        modeloBotaoNPC.gameObject.SetActive(false);

        int indice = 0;
        foreach (garson npc in equipe)
        {
            if (npc == null) continue;
            if (npc.ehCliente) continue; // cliente nao entra na equipe

            Button botao = Instantiate(modeloBotaoNPC, conteinerNPCs);
            botao.gameObject.SetActive(true);

            // Empilha os botoes de cima pra baixo dentro do painel
            RectTransform rt = botao.transform as RectTransform;
            if (rt != null) rt.anchoredPosition = new Vector2(0f, -10f - indice * 52f);

            Text rotulo = botao.GetComponentInChildren<Text>(true);
            if (rotulo != null) rotulo.text = npc.name;

            garson capturado = npc; // captura pro lambda
            botao.onClick.AddListener(() => EnviarNPC(capturado));
            indice++;
        }

        botoesCriados = true;
    }

    // ===============================================================
    //  FLUXO: local -> NPC
    // ===============================================================

    // Jogador tocou num local do mapinha
    public void SelecionarPonto(int indice)
    {
        if (indice < 0 || indice >= pontos.Length) return;

        pontoSelecionado = indice;
        if (painelEscolherNPC != null) painelEscolherNPC.SetActive(true);

        Debug.Log("[Gerenciamento] Local escolhido no mapa: '" + pontos[indice].nome +
                  "'. Agora escolha o NPC da equipe.");
    }

    // Jogador escolheu o NPC -> manda para o local selecionado
    public void EnviarNPC(garson npc)
    {
        if (npc == null) return;

        // Cliente (tag "cliente") nao recebe tarefas da equipe
        if (npc.ehCliente)
        {
            Debug.LogWarning("[Gerenciamento] '" + npc.name + "' e cliente (tag 'cliente'): nao recebe tarefas da equipe.");
            return;
        }

        if (pontoSelecionado < 0)
        {
            Debug.LogWarning("[Gerenciamento] Escolha primeiro um local no mapa.");
            return;
        }

        PontoDoMapa ponto = pontos[pontoSelecionado];
        if (ponto.objeto == null)
        {
            Debug.LogWarning("[Gerenciamento] O ponto '" + ponto.nome + "' nao tem objeto no cenario.");
            return;
        }

        // Se o NPC ja tinha um pedido em andamento, substitui
        pedidos.RemoveAll(p => p.npc == npc);

        npc.MandarPara(ponto.objeto.transform.position); // NPC comeca a andar
        pedidos.Add(new Pedido { npc = npc, ponto = ponto });

        if (painelEscolherNPC != null) painelEscolherNPC.SetActive(false);
        pontoSelecionado = -1;

        Debug.Log("[Gerenciamento] NPC '" + npc.name + "' foi mandado para '" + ponto.nome + "'.");

        // Fecha a tela e destpausa o jogo: assim o jogador VE o NPC indo
        // pro local escolhido (com a tela aberta o jogo fica pausado)
        Fechar();
    }

    private void Update()
    {
        // Tecla M abre a tela de gerenciamento de equipes
        // (Abrir() ja bloqueia se o menu ou o minigame estiverem abertos)
        if (!Aberto && Input.GetKeyDown(KeyCode.M))
            Abrir();

        // Quando o NPC chega no local, faz a tarefa e pega o DeuCerto
        for (int i = pedidos.Count - 1; i >= 0; i--)
        {
            Pedido pedido = pedidos[i];

            if (pedido.npc == null)
            {
                pedidos.RemoveAt(i);
                continue;
            }

            if (!pedido.npc.chegou) continue; // ainda a caminho

            pedido.npc.chegou = false;
            pedidos.RemoveAt(i);

            // Tarefa + resultado (bool DeuCerto) via controlador de interacoes
            if (controlador != null)
                controlador.InteragirNPC(pedido.npc, pedido.ponto.statusTarefa, pedido.ponto.objeto);
            else
                pedido.npc.fazerTarefas(pedido.ponto.statusTarefa);

            Debug.Log("[Gerenciamento] Tarefa '" + pedido.ponto.statusTarefa + "' concluida por '" +
                      pedido.npc.name + "' -> DeuCerto: " + pedido.npc.DeuCerto);

            // Sem ordem o NPC fica em idle (a patrulha foi removida)
            if (voltarParaIdleAposTarefa)
                pedido.npc.VoltarParaIdle();
        }
    }

    private void LateUpdate()
    {
        // Esc fecha a tela (LateUpdate = depois dos Updates, entao o
        // MenuSistema nao abre o menu junto no mesmo Esc)
        if (Aberto && Input.GetKeyDown(KeyCode.Escape))
            Fechar();
    }

    // ===============================================================
    //  ABRIR / FECHAR (chamar de um botao, ex.: "GERENCIAR")
    // ===============================================================
    public void Abrir()
    {
        if (Aberto || MinigameCozinha.JogoAberto || MenuSistema.MenuAberto) return;

        Aberto = true;
        pontoSelecionado = -1;
        CriarBotoesNPC(); // (re)cria os botoes se no Start ainda nao dava

        if (pausarJogo)
        {
            timeScaleAntes = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (painelGerenciamento != null) painelGerenciamento.SetActive(true);
        if (painelEscolherNPC != null) painelEscolherNPC.SetActive(false);
    }

    public void Fechar()
    {
        if (!Aberto) return;

        Aberto = false;
        Time.timeScale = (timeScaleAntes <= 0f) ? 1f : timeScaleAntes;

        if (painelGerenciamento != null) painelGerenciamento.SetActive(false);
        if (painelEscolherNPC != null) painelEscolherNPC.SetActive(false);
    }

    // Atalho pra Inspector: Botao OnClick -> GerenciadorTarefas > Abrir/Fechar
    public void Alternar()
    {
        if (Aberto) Fechar();
        else Abrir();
    }
}
