using System.Collections.Generic;
using UnityEngine;

// GERENCIADOR UNICO DE INTERACOES - coloque em um objeto VAZIO da cena.
//
// JOGADOR: a "esfera" (raio) gira em torno do objeto "Body" dentro do player.
//   Quando um objeto com a TAG "objetoIteragivel" entrar dentro dela, a
//   interacao e ativada (botao aparece + tecla E abre o minigame).
//
// NPCs: quando um NPC (componente "garson") chega perto de um objeto com a
//   tag, o controlador chama npc.fazerTarefas(status) e pega o resultado
//   (bool DeuCerto) em DeuCertoNPC.
//
// Os objetos interativos NAO precisam de script: Collider2D + essa tag.
public class ControladorInteracao : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Opcional: botao visual (world space) que acompanha o jogador")]
    public GameObject botaoInteracao;
    [Tooltip("Deixe vazio para achar o jogador automaticamente")]
    public Transform jogador;
    public Vector3 deslocamento = new Vector3(1f, 0.5f, 0f);

    [Header("Esfera de deteccao (ao redor do jogador)")]
    [Tooltip("Raio da esfera onde vale a interacao")]
    public float raio = 2f;
    [Tooltip("Tag que o objeto interativo precisa ter")]
    public string tagObjeto = "objetoIteragivel";
    public bool deteccaoAutomatica = true;
    [Tooltip("Congela deteccao e tecla E com o menu ou o minigame abertos")]
    public bool bloquearComPaineisAbertos = true;

    [Header("Tarefas dos NPCs")]
    [Tooltip("Status usado quando o NPC interage (nome do case do fazerTarefas)")]
    public string statusTarefaNPC = "manusear";
    [Tooltip("Detecta NPCs (componente 'garson') perto dos objetos sozinho")]
    public bool detectarNPCs = true;
    [Tooltip("Resultado da ultima tarefa (copiado do DeuCerto do NPC)")]
    public bool DeuCertoNPC;
    [Tooltip("Ultimo NPC / objeto que interagiram")]
    public garson ultimoNPC;
    public GameObject ultimoObjeto;

    // Objeto interativo mais proximo dentro da esfera (para usar em codigo)
    public GameObject ObjetoAtual { get; private set; }

    private bool podeInteragir = false;
    private bool tagValida = true;
    private Transform corpoPlayer;   // objeto "Body" dentro do player (pivo do circulo)
    private bool corpoAvisado = false;

    // NPCs ja vistos perto de cada objeto (dispara so na entrada)
    private readonly Dictionary<garson, GameObject> npcProximo = new Dictionary<garson, GameObject>();
    private float cronometroNPCs = 0f;
    private const float INTERVALO_CHECAGEM_NPC = 0.5f;

    private void Start()
    {
        if (botaoInteracao != null) botaoInteracao.SetActive(false);

        // Confere se a tag existe (CompareTag lanca erro se nao existir)
        try
        {
            CompareTag(tagObjeto);
        }
        catch (System.Exception)
        {
            tagValida = false;
            Debug.LogError("[Interacao] A tag '" + tagObjeto + "' nao existe! Crie em Edit > Project Settings > Tags and Layers.");
        }
    }

    private void Update()
    {
        if (jogador == null)
        {
            var controle = FindAnyObjectByType<Controlapersonagem>();
            if (controle != null) jogador = controle.transform;
            if (jogador == null) return;
        }

        // Com menu/minigame/gerenciamento abertos, nao detecta nem usa o E
        if (bloquearComPaineisAbertos && (MinigameCozinha.JogoAberto || MenuSistema.MenuAberto || GerenciadorTarefas.Aberto))
            return;

        if (deteccaoAutomatica)
            AtualizarDeteccao();

        // Tarefas dos NPCs (pausam junto com o menu, por estar apos o bloqueio)
        if (detectarNPCs)
            AtualizarNPCs();

        // Mantem o botao ao lado do jogador
        if (podeInteragir && botaoInteracao != null)
            botaoInteracao.transform.position = jogador.position + deslocamento;

        // Tecla E interage
        if (podeInteragir && Input.GetKeyDown(KeyCode.E))
            Interagir();
    }

    private void AtualizarDeteccao()
    {
        if (!tagValida) return;

        GameObject novo = null;
        float melhorDistancia = float.MaxValue;

        // A "esfera" gira em torno do objeto "Body" dentro do player (pivo do circulo):
        // so conta quem estiver dentro E com a tag
        Collider2D[] encontrados = Physics2D.OverlapCircleAll(CentroDoCirculo(), raio);
        foreach (Collider2D col in encontrados)
        {
            if (col == null) continue;
            if (Ignorar(col)) continue;
            if (!col.CompareTag(tagObjeto)) continue;

            float distancia = Vector2.Distance(jogador.position, col.transform.position);
            if (distancia < melhorDistancia)
            {
                melhorDistancia = distancia;
                novo = col.gameObject;
            }
        }

        if (novo == ObjetoAtual) return;

        ObjetoAtual = novo;

        // Dentro da esfera com a tag -> interacao ativada; saiu -> desativada
        if (novo != null) MostrarBotao();
        else EsconderBotao();
    }

    // ===============================================================
    //  INTERACAO DOS NPCs
    //  Procura NPCs (componente "garson") perto de um objeto com a tag.
    //  Quando o NPC CHEGA perto, chama npc.fazerTarefas(...) e pega o
    //  resultado (bool DeuCerto) em DeuCertoNPC. Dispara so na entrada
    //  (quando sai de perto, rearma para a proxima vez).
    // ===============================================================
    private void AtualizarNPCs()
    {
        if (!tagValida) return;

        // Checa em intervalos para nao varrer a cena toda quadro
        cronometroNPCs -= Time.deltaTime;
        if (cronometroNPCs > 0f) return;
        cronometroNPCs = INTERVALO_CHECAGEM_NPC;

        foreach (garson npc in FindObjectsByType<garson>(FindObjectsSortMode.None))
        {
            if (npc == null) continue;

            GameObject objeto = EncontrarObjetoPerto(npc.transform.position);
            npcProximo.TryGetValue(npc, out GameObject anterior);

            if (objeto == anterior) continue; // nada mudou desde a ultima checagem

            if (objeto != null)
            {
                // NPC chegou perto do objeto: faz a tarefa e pega o resultado
                npcProximo[npc] = objeto;
                InteragirNPC(npc, statusTarefaNPC, objeto);
            }
            else
            {
                // NPC saiu de perto: rearma a tarefa para a proxima chegada
                npcProximo.Remove(npc);
            }
        }
    }

    // Objeto "objetoIteragivel" mais proximo de uma posicao (ou null)
    private GameObject EncontrarObjetoPerto(Vector3 posicao)
    {
        GameObject melhor = null;
        float melhorDistancia = float.MaxValue;

        Collider2D[] encontrados = Physics2D.OverlapCircleAll(posicao, raio);
        foreach (Collider2D col in encontrados)
        {
            if (col == null) continue;
            if (!col.CompareTag(tagObjeto)) continue;

            float distancia = Vector2.Distance(posicao, col.transform.position);
            if (distancia < melhorDistancia)
            {
                melhorDistancia = distancia;
                melhor = col.gameObject;
            }
        }

        return melhor;
    }

    // Chamado pela deteccao automatica OU por voce, quando um NPC interage
    // com o objeto: roda o fazerTarefas do NPC e pega o bool DeuCerto.
    // Ex.: InteragirNPC(meunpc, "grelha", mesa);
    public bool InteragirNPC(garson npc, string statusTarefa, GameObject objeto = null)
    {
        if (npc == null)
        {
            Debug.LogWarning("[Interacao] InteragirNPC chamado sem NPC.");
            DeuCertoNPC = false;
            return false;
        }

        npc.fazerTarefas(statusTarefa);  // switch + sorteio la no script do NPC
        DeuCertoNPC = npc.DeuCerto;      // pega o resultado da tarefa

        ultimoNPC = npc;
        ultimoObjeto = objeto;

        Debug.Log("[Interacao] NPC '" + npc.name + "' fez a tarefa '" + statusTarefa +
                  "' (Starefa = " + npc.Starefa + ") -> DeuCerto: " + DeuCertoNPC);

        return DeuCertoNPC;
    }

    // Pivo do circulo: o objeto "Body" dentro do player (procura tambem nos filhos
    // mais fundos); se nao achar, usa a propria posicao do player.
    private Vector3 CentroDoCirculo()
    {
        if (jogador == null) return transform.position;

        if (corpoPlayer == null)
        {
            // Filho direto chamado "Body"
            corpoPlayer = jogador.Find("Body");

            // Se nao for filho direto, procura em qualquer nivel abaixo
            if (corpoPlayer == null)
            {
                foreach (Transform t in jogador.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Body")
                    {
                        corpoPlayer = t;
                        break;
                    }
                }
            }

            if (corpoPlayer == null && !corpoAvisado)
            {
                corpoAvisado = true;
                Debug.LogWarning("[Interacao] Objeto 'Body' nao encontrado dentro do player; usando a posicao do player como pivo.");
            }
        }

        return corpoPlayer != null ? corpoPlayer.position : jogador.position;
    }

    // Nao considera o proprio jogador, o botao nem o controlador
    private bool Ignorar(Collider2D col)
    {
        if (jogador != null && col.transform.IsChildOf(jogador)) return true;
        if (botaoInteracao != null && col.transform.IsChildOf(botaoInteracao.transform)) return true;
        if (col.transform.IsChildOf(transform)) return true;
        if (col.CompareTag("Player")) return true;
        return col.GetComponentInParent<Controlapersonagem>() != null;
    }

    public void MostrarBotao()
    {
        podeInteragir = true;
        if (botaoInteracao != null) botaoInteracao.SetActive(true);
    }

    public void EsconderBotao()
    {
        podeInteragir = false;
        if (botaoInteracao != null) botaoInteracao.SetActive(false);
    }

    // Tecla E (ou botao de UI) -> abre o minigame
    public void Interagir()
    {
        if (!podeInteragir) return;

        if (MinigameCozinha.Instancia != null)
            MinigameCozinha.Instancia.Abrir();
        else
            Debug.LogWarning("[Interacao] MinigameCozinha nao encontrado no Canvas _ Sistema.");
    }

    // Desenha a esfera no Scene View (nao aparece no jogo) - mesmo pivo do OverlapCircleAll
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(CentroDoCirculo(), raio);
    }
}
