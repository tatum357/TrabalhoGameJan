using UnityEngine;
using UnityEngine.UI;

// Gerencia APENAS o pop-up do minigame no canvas (abrir/fechar).
// Quem decide quando interagir e o ControladorInteracao (tecla E perto do objeto).
// Anexado ao "Canvas _ Sistema".
public class MinigameCozinha : MonoBehaviour
{
    [Header("UI do Canvas (preenchida)")]
    public GameObject painelMinigame; // pop-up do minigame
    public Button btnFechar;

    // Para o menu (Esc) e o ControladorInteracao saberem o estado
    public static bool JogoAberto { get; private set; }
    public static MinigameCozinha Instancia { get; private set; }

    private int frameAbertura = -1;

    private void Awake()
    {
        Instancia = this;
        JogoAberto = false;

        if (painelMinigame != null) painelMinigame.SetActive(false);
        if (btnFechar != null) btnFechar.onClick.AddListener(Fechar);
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

        // Non fecha no mesmo frame em que abriu (a mesma tecla E que abriu)
        if (Time.frameCount == frameAbertura) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
            Fechar();
    }

    public void Abrir()
    {
        if (JogoAberto || MenuSistema.MenuAberto) return;
        JogoAberto = true;
        frameAbertura = Time.frameCount;
        if (painelMinigame != null) painelMinigame.SetActive(true);
    }

    public void Fechar()
    {
        if (!JogoAberto) return;
        JogoAberto = false;
        if (painelMinigame != null) painelMinigame.SetActive(false);
    }
}
