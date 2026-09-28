using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class garson : MonoBehaviour
{
    // ===============================================================
    //  STATUS DO NPC
    //  Todos os status sao floats e ficam dentro de "status".
    //  Cada papel (chefe / faxineiro / garcom) usa um subconjunto
    //  diferente desses status - veja os metodos la embaixo.
    // ===============================================================
    [Serializable]
    public class Status
    {
        [Header("Compartilhados")]
        public float equilibrio = 5f;              // estabilidade do NPC
        public float manusear = 5f;                // habilidade de manipular objetos
        public float velocidadeDeMovimento = 2f;   // velocidade de andar

        [Header("Somente do chefe")]
        public float paladar = 5f;                 // sensibilidade do paladar
        public float velocidadeDeCorte = 5f;       // velocidade do corte
        public float grelha = 5f;                  // tempo/habilidade na grelha
    }

    [Header("Status do NPC")]
    public Status status = new Status();

    // Destino mandado pelo player (tela de gerenciamento).
    // Nao existe patrulha: sem ordem o NPC fica em idle.
    private Vector2 target;

    [Header("Movimento")]
    [Tooltip("Velocidade de andar ate o destino mandado")]
    public float velocidade = 2;
    [Tooltip("Tempo calculado pelos controllers de papel (chefe/faxineiro/garcom)")]
    public float pausa = 1.5f;

    // Tags que definem o papel do NPC (criadas em Project Settings > Tags)
    private const string TAG_CHEFE = "chefe";
    private const string TAG_FAXINEIRO = "faxineiro";
    private const string TAG_GARCON = "gar\u00E7on"; // "garçon" (escape p/ nao depender do encoding do arquivo)
    private const string TAG_GARCON_ALT = "garcom"; // alternativa sem cedilha

    // Distancia minima para considerar que chegou no ponto da patrulha.
    // So o GarconControler mexe nisso (equilibrio).
    private float toleranciaParada = 1f;

    private Rigidbody2D rb;
    private Animator anim;

    public float Starefa;   // valor do status comparado no switch (0 a 10)
    public bool DeuCerto;   // resultado da chance (true = acertou)

    [Header("Tarefas enviadas pelo player")]
    public bool sobControle = false;   // true = vai pro destino mandado; senao fica em idle
    public bool chegou = false;        // vira true quando chega no destino mandado
    private bool mostreiIdle;          // controla tocar a anim de idle so uma vez

    [Header("Colisao")]
    [Tooltip("Se marcado, o jogador atravessa a 'esfera' deste NPC (o mundo continua bloqueando)")]
    public bool jogadorAtravessa = true;

    private bool colisaoAplicada;      // true quando o player ja foi ignorado (ou nao ha o que fazer)
    private float cronometroColisao;   // espera entre tentativas de ignorar a colisao

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        colisaoAplicada = IgnorarColisaoComJogador();
    }

    void Update()
    {
        // Escolhe o controller pela tag do GameObject e aplica os status.
        AplicarController();

        // Se a colisao com o jogador ainda nao foi ignorada (ex.: o player
        // so apareceu/depois foi ativado), tenta de novo de vez em quando.
        if (!colisaoAplicada)
        {
            cronometroColisao -= Time.unscaledDeltaTime;
            if (cronometroColisao <= 0f)
            {
                cronometroColisao = 1f;
                colisaoAplicada = IgnorarColisaoComJogador();
            }
        }

        // SEM ORDEM DO JOGADOR: fica em idle (parado no lugar). O NPC NAO
        // vai pra nenhum ponto fixo - nao existe mais patrulha.
        if (!sobControle)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (!mostreiIdle)
            {
                mostreiIdle = true;
                if (anim != null) anim.Play("idle");
            }
            return;
        }

        // COM ORDEM: caminha ate o destino escolhido pelo player
        if (Vector2.Distance(transform.position, target) < toleranciaParada)
        {
            // Chegou no local mandado: para, sinaliza (chegou) e espera a tarefa
            chegou = true;
            if (rb != null) rb.velocity = Vector2.zero;
            if (!mostreiIdle)
            {
                mostreiIdle = true;
                if (anim != null) anim.Play("idle");
            }
            return;
        }

        Vector2 Direction = ((Vector3)target - transform.position).normalized;
        if (Direction.x < 0 && transform.localScale.x > 0 || Direction.x > 0 && transform.localScale.x < 0)
            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);

        if (rb != null) rb.velocity = Direction * velocidade;
    }

    // ===============================================================
    //  COLISAO - o jogador atravessa a "esfera" do NPC
    //  O colisor do NPC continua valendo contra o mundo (parede, banca,
    //  barreira); so o jogador que ignora ele.
    // ===============================================================
    // Retorna true quando nao ha mais nada a ignorar; false se o player ainda
    // nao existe (ai o Update tenta de novo daqui a 1s).
    private bool IgnorarColisaoComJogador()
    {
        if (!jogadorAtravessa) return true;   // desligado no Inspector: nada a fazer

        Controlapersonagem controle = FindAnyObjectByType<Controlapersonagem>();
        if (controle == null) return false;   // player ainda nao existe/ta inativo

        Collider2D[] meusColisores = GetComponentsInChildren<Collider2D>();
        Collider2D[] colisoresJogador = controle.GetComponentsInChildren<Collider2D>();
        if (meusColisores.Length == 0 || colisoresJogador.Length == 0)
            return true;                      // sem colisores = nao ha bloqueio

        foreach (Collider2D meu in meusColisores)
        {
            foreach (Collider2D doJogador in colisoresJogador)
            {
                Physics2D.IgnoreCollision(doJogador, meu, true);
            }
        }

        return true;
    }

    // ===============================================================
    //  DISTRIBUIDOR: ve a tag do GameObject e chama o controller certo
    // ===============================================================
    private void AplicarController()
    {
        // padrao, caso o NPC nao tenha nenhuma tag de papel
        toleranciaParada = 1f;

        string tag = gameObject.tag;

        if (tag == TAG_CHEFE)
            chefeControler();
        else if (tag == TAG_FAXINEIRO)
            FaxineiroControler();
        else if (tag == TAG_GARCON || tag == TAG_GARCON_ALT)
            GarconControler();
    }

    // ===============================================================
    //  CHEFE  -  tag "chefe"
    //  Usa: paladar, manusear, velocidade de corte e grelha
    // ===============================================================
    public void chefeControler()
    {
        float manuseio = Mathf.Max(0f, status.manusear);
        float corte = Mathf.Max(0f, status.velocidadeDeCorte);
        float grelha = Mathf.Max(0f, status.grelha);
        float paladar = Mathf.Max(0f, status.paladar);

        // Base de manuseio dos ingredientes
        float baseManuseio = manuseio * 0.10f;

        // Quanto mais rapido o corte, menor a parada para cortar
        float tempoCorte = 8f / Mathf.Max(1f, corte);

        // Quanto mais tempo de grelha, maior a parada na frente da grelha
        float tempoGrelha = grelha * 0.25f;

        // Melhor o paladar, mais tempo dedicado a provar o prato
        float prova = paladar * 0.15f;

        // O chefe nao usa velocidade de movimento nem equilibrio
        pausa = Mathf.Max(0.25f, baseManuseio + tempoCorte + tempoGrelha + prova);
    }

    // ===============================================================
    //  FAXINEIRO  -  tag "faxineiro"
    //  Usa: velocidade de movimento e manusear
    // ===============================================================
    public void FaxineiroControler()
    {
        // Quanto anda entre os pontos da rota
        velocidade = Mathf.Max(0f, status.velocidadeDeMovimento);

        // Quanto tempo para para limpar em cada ponto
        pausa = Mathf.Max(0.25f, status.manusear * 0.10f);
    }

    // ===============================================================
    //  GARCOM  -  tag "garçon"
    //  Usa: velocidade de movimento, equilibrio e manusear
    // ===============================================================
    public void GarconControler()
    {
        // Quanto anda entre as mesas
        velocidade = Mathf.Max(0f, status.velocidadeDeMovimento);

        // Equilibrio: quanto maior, mais perto do ponto ele consegue
        // parar sem "passar do lado" (bandeja nao cai)
        toleranciaParada = Mathf.Clamp(0.25f + status.equilibrio * 0.10f, 0.25f, 5f);

        // Manusear: tempo parado servindo a mesa
        pausa = Mathf.Max(0.10f, status.manusear * 0.15f);
    }

    // ===============================================================
    //  MANDADO PELO PLAYER (tela de gerenciamento)
    // ===============================================================
    public void MandarPara(Vector2 destino)
    {
        // So este destino vale: ele NAO volta pra rota/patrulha nenhuma
        sobControle = true;
        chegou = false;
        target = destino;
        mostreiIdle = false;              // deixa tocar a anim de andar
        if (rb != null) rb.velocity = Vector2.zero;
        if (anim != null) anim.Play("Walk");
    }

    // Sem ordem nenhuma: NPC volta pro idle (parado, nao vai pra lugar nenhum)
    public void VoltarParaIdle()
    {
        sobControle = false;
        chegou = false;
        mostreiIdle = false;              // toca o idle de novo uma vez
        if (rb != null) rb.velocity = Vector2.zero;
    }

    public void fazerTarefas(string statusTarefa)
    {
        // 1) Switch no inicio: compara com os status
        switch (statusTarefa)
        {
            case "equilibrio": Starefa = status.equilibrio; break;
            case "manusear": Starefa = status.manusear; break;
            case "velocidadeDeMovimento": Starefa = status.velocidadeDeMovimento; break;
            case "paladar": Starefa = status.paladar; break;
            case "velocidadeDeCorte": Starefa = status.velocidadeDeCorte; break;
            case "grelha": Starefa = status.grelha; break;
            default:
                Starefa = 0f;
                Debug.LogWarning("[NPC] fazerTarefas: status desconhecido '" + statusTarefa +
                    "' - use: equilibrio, manusear, velocidadeDeMovimento, paladar, velocidadeDeCorte ou grelha.");
                break;
        }

        float resultado = UnityEngine.Random.Range(0f, 10f);

        if (resultado < 10f - Starefa)
            DeuCerto = false;
        else
            DeuCerto = true;
    }
}
