using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

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

    [Header("Patrulha")]
    public Vector2[] patrolpoint;
    private int currentPatrolIndex;
    private Vector2 target;
    public float pausa = 1.5f;
    private bool inpause;
    public float velocidade = 2;

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
    public bool sobControle = false;   // true = segue o destino mandado, nao a patrulha
    public bool chegou = false;        // vira true quando chega no destino mandado

    [Header("Colisao")]
    [Tooltip("Se marcado, o jogador atravessa a 'esfera' deste NPC (o mundo continua bloqueando)")]
    public bool jogadorAtravessa = true;

    [Header("NavMesh")]
    [Tooltip("Se marcado, o NPC vai aos pontos mandados pelo player pelo NavMesh (NavMech2D), contornando os obstaculos. Sem NavMesh pronto, ele faz o movimento reto de sempre")]
    public bool usarNavMesh = true;

    private Coroutine rotinaPatrulha;

    private NavMeshAgent agente;
    private bool usandoNavMesh;       // esta indo pro destino pelo NavMesh agora
    private bool destinoNavPendente;  // tem destino novo a mandar pro agente
    private Vector2 ultimoDestinoNav;
    private bool avisoNavDado;        // aviso de "sem NavMesh" ja mostrado (1x)

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        IgnorarColisaoComJogador();

        // NavMesh: o controle do agente e nosso. Ele so liga quando o
        // player manda o NPC pra algum lugar (assim nao briga com a fisica)
        agente = GetComponent<NavMeshAgent>();
        if (agente != null)
        {
            agente.updateRotation = false;  // o agente nao pode girar o sprite
            agente.updateUpAxis = false;    // exigencia do NavMeshPlus (2D)
            agente.enabled = false;
        }

        rotinaPatrulha = StartCoroutine(setpatrolpoint());
    }

    void Update()
    {
        // Escolhe o controller pela tag do GameObject e aplica os status.
        AplicarController();

        if (inpause)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            DesativarAgente();
            return;
        }

        Vector2 Direction = ((Vector3)target - transform.position).normalized;

        // Com ordem do player, tenta ir pelo NavMesh (contornando os
        // obstaculos). Se nao der, cai no movimento reto de sempre.
        usandoNavMesh = sobControle && MoverPorNavMesh();

        if (usandoNavMesh)
        {
            // Quem move o NPC agora e o NavMeshAgent
            if (rb != null) rb.velocity = Vector2.zero;
            VirarSprite(agente.velocity.sqrMagnitude > 0.001f ? agente.velocity.x : Direction.x);
        }
        else
        {
            VirarSprite(Direction.x);
            if (rb != null) rb.velocity = Direction * velocidade;
        }

        // Chegou: perto o bastante OU o agente terminou o caminho dele
        // (o destino pode ter sido arrumado pro NavMesh mais proximo)
        bool chegouNoDestino = Vector2.Distance(transform.position, target) < toleranciaParada
            || (usandoNavMesh && agente.hasPath && !agente.pathPending && agente.remainingDistance <= 0.05f);

        if (chegouNoDestino)
        {
            if (sobControle)
            {
                // Chegou no local mandado pelo player: para e sinaliza
                chegou = true;
                inpause = true;
                if (rb != null) rb.velocity = Vector2.zero;
                DesativarAgente();
                if (anim != null) anim.Play("idle");
            }
            else
            {
                rotinaPatrulha = StartCoroutine(setpatrolpoint());
            }
        }
    }

    // ===============================================================
    //  SPRITE - vira o sprite pra direcao que esta andando
    // ===============================================================
    private void VirarSprite(float eixoX)
    {
        if (eixoX < 0 && transform.localScale.x > 0 || eixoX > 0 && transform.localScale.x < 0)
            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    // ===============================================================
    //  NAVMESH - o NPC vai pros pontos marcados pelo player pelo
    //  NavMesh (NavMech2D + NavMeshPlus), contornando os obstaculos.
    //  Se nao tiver NavMesh pronto, tudo cai no movimento reto de
    //  sempre, entao o jogo continua igual mesmo antes do Bake.
    // ===============================================================

    // Liga o agente e cola ele no NavMesh. Retorna false se nao der
    // (sem componente, NavMesh desligado ou NPC fora do NavMesh).
    private bool AtivarAgente()
    {
        if (agente == null)
        {
            agente = GetComponent<NavMeshAgent>();
            if (agente == null) return false;
            agente.updateRotation = false;
            agente.updateUpAxis = false;
        }

        if (!usarNavMesh)
        {
            DesativarAgente(); // desliga o agente se ainda estiver ligado
            return false;
        }

        agente.enabled = true;

        if (!agente.isOnNavMesh)
        {
            // Cola no NavMesh mais perto (serve pra quem nasceu um pouco fora)
            if (!(NavMesh.SamplePosition(transform.position, out NavMeshHit perto, 3f, NavMesh.AllAreas)
                  && agente.Warp(perto.position)))
            {
                AvisoNav();
                agente.enabled = false;
                return false;
            }
        }

        agente.speed = Mathf.Max(0f, velocidade);
        return true;
    }

    // Desliga o agente e devolve o controle pro movimento reto.
    private void DesativarAgente()
    {
        destinoNavPendente = false;
        if (agente != null && agente.enabled) agente.enabled = false;
    }

    // Aviso unico no console quando o NavMesh nao esta disponivel
    // (sem encher o log de repeticao).
    private void AvisoNav()
    {
        if (avisoNavDado) return;
        avisoNavDado = true;
        Debug.LogWarning("[garson] NavMesh indisponivel - usando movimento reto. Confira se o 'NavMech2D' tem o componente 'Navigation CollectSources2d' e se voce clicou em Bake nele.", this);
    }

    // Tenta levar o NPC pro target pelo NavMesh. Retorna false quando nao
    // da pra usar (sem NavMesh / sem caminho) - ai quem anda la no Update
    // e o movimento reto de sempre.
    private bool MoverPorNavMesh()
    {
        if (!usarNavMesh || agente == null || !agente.enabled) return false;

        if (!agente.isOnNavMesh)
        {
            // Saiu (ou nunca colou) no NavMesh: nao insiste
            AvisoNav();
            DesativarAgente();
            return false;
        }

        agente.speed = Mathf.Max(0f, velocidade);

        // Manda o destino so quando ele mudou (ordem nova do player)
        if (destinoNavPendente || target != ultimoDestinoNav)
        {
            destinoNavPendente = false;
            ultimoDestinoNav = target;

            if (!NavMesh.SamplePosition(target, out NavMeshHit perto, 5f, NavMesh.AllAreas))
            {
                // Nenhum NavMesh perto do ponto pedido
                AvisoNav();
                DesativarAgente();
                return false;
            }

            Vector3 alvo = perto.position;

            // Bug conhecido do NavMeshAgent: ele trava quando o caminho
            // e reto em Y (X identico ao do destino). Arrasta 0.0001 pro
            // lado pra nunca acontecer.
            if (Mathf.Abs(alvo.x - transform.position.x) < 0.0001f)
                alvo.x += 0.0001f;

            if (agente.isStopped) agente.isStopped = false;

            if (!agente.SetDestination(alvo))
            {
                DesativarAgente();
                return false;
            }
        }

        // Caminho valido ou ainda calculando = da pra usar;
        // terminou sem caminho = devolve pro movimento reto
        return agente.hasPath || agente.pathPending;
    }

    // ===============================================================
    //  COLISAO - o jogador atravessa a "esfera" do NPC
    //  O colisor do NPC continua valendo contra o mundo (parede, banca,
    //  barreira); so o jogador que ignora ele.
    // ===============================================================
    private void IgnorarColisaoComJogador()
    {
        if (!jogadorAtravessa) return;

        Controlapersonagem controle = FindAnyObjectByType<Controlapersonagem>();
        if (controle == null) return;

        Collider2D[] meusColisores = GetComponentsInChildren<Collider2D>();
        Collider2D[] colisoresJogador = controle.GetComponentsInChildren<Collider2D>();
        if (meusColisores.Length == 0 || colisoresJogador.Length == 0) return;

        foreach (Collider2D meu in meusColisores)
        {
            foreach (Collider2D doJogador in colisoresJogador)
            {
                Physics2D.IgnoreCollision(doJogador, meu, true);
            }
        }
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
    //  Patrulha
    // ===============================================================
    IEnumerator setpatrolpoint()
    {
        if (patrolpoint == null || patrolpoint.Length == 0)
        {
            // Sem pontos de patrulha: fica parado onde esta
            // (sem isso o NPC andaria reto ate a origem do mundo)
            inpause = true;
            if (anim != null) anim.Play("idle");
            yield break;
        }

        inpause = true;
        if (anim != null) anim.Play("idle");

        yield return new WaitForSeconds(pausa);

        currentPatrolIndex = (currentPatrolIndex + 1) % patrolpoint.Length;
        target = patrolpoint[currentPatrolIndex];
        inpause = false;

        if (anim != null) anim.Play("Walk");
    }

    // ===============================================================
    //  MANDADO PELO PLAYER (tela de gerenciamento)
    // ===============================================================
    public void MandarPara(Vector2 destino)
    {
        // Corta a pausa/atual da patrulha e comeca a ir pro destino
        if (rotinaPatrulha != null)
        {
            StopCoroutine(rotinaPatrulha);
            rotinaPatrulha = null;
        }

        sobControle = true;
        chegou = false;
        target = destino;
        inpause = false;
        if (anim != null) anim.Play("Walk");

        // Tenta ir pelo NavMesh; se nao der, o Update faz o movimento reto
        destinoNavPendente = AtivarAgente();
    }

    // Volta a andar na rota normal (patrulha)
    public void VoltarParaIdle()
    {
        sobControle = false;
        chegou = false;
        DesativarAgente(); // volta pro movimento reto (patrulha)
        rotinaPatrulha = StartCoroutine(setpatrolpoint());
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
            default: Starefa = 0f; break;
        }

        float resultado = UnityEngine.Random.Range(0f, 10f);

        if (resultado < 10f - Starefa)
            DeuCerto = false;
        else
            DeuCerto = true;
    }
}
