using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Painel de sistema (Esc): Save, Exit (volta pro menu) e Volume.
// O E agora abre o minigame (ver MinigameCozinha).
// Anexado ao "Canvas _ Sistema". Wires dos botoes/slider sao feitos em codigo.
public class MenuSistema : MonoBehaviour
{
    [Header("Painel do Sistema (Esc abre/fecha)")]
    [SerializeField] private GameObject painel;
    [SerializeField] private Button btnSalvar;
    [SerializeField] private Button btnSair;
    [SerializeField] private Slider sliderVolume;
    [SerializeField] private Text txtVolume;
    [SerializeField] private Text txtFeedback;
    [SerializeField] private string cenaMenu = "Menu";

    private bool aberto;
    private float timeScaleAntes = 1f;

    // Para o minigame saber se o menu esta aberto
    public static bool MenuAberto { get; private set; }

    private void Awake()
    {
        MenuAberto = false;
        if (painel != null) painel.SetActive(false);

        AjustarFontes();

        if (btnSalvar != null) btnSalvar.onClick.AddListener(Salvar);
        if (btnSair != null) btnSair.onClick.AddListener(Sair);

        float vol = PlayerPrefs.GetFloat("volume", 1f);
        AudioListener.volume = vol;
        if (sliderVolume != null)
        {
            sliderVolume.SetValueWithoutNotify(vol);
            sliderVolume.onValueChanged.AddListener(AlterarVolume);
        }
        MostrarVolume(vol);
    }

    private void Update()
    {
        // Esc abre/fecha as opcoes. E agora e do minigame (nao abre menu com E)
        if (Input.GetKeyDown(KeyCode.Escape) && !MinigameCozinha.JogoAberto)
            Alternar();
    }

    public void Alternar()
    {
        if (aberto) Fechar();
        else Abrir();
    }

    public void Abrir()
    {
        if (painel == null || aberto) return;
        aberto = true;
        MenuAberto = true;
        timeScaleAntes = Time.timeScale;
        Time.timeScale = 0f; // pausa o jogo (UI continua funcionando)
        painel.SetActive(true);
        Feedback("");
    }

    public void Fechar()
    {
        if (painel == null || !aberto) return;
        aberto = false;
        MenuAberto = false;
        Time.timeScale = (timeScaleAntes <= 0f) ? 1f : timeScaleAntes;
        painel.SetActive(false);
    }

    public void AlterarVolume(float valor)
    {
        AudioListener.volume = valor;
        PlayerPrefs.SetFloat("volume", valor);
        MostrarVolume(valor);
    }

    private void MostrarVolume(float valor)
    {
        if (txtVolume != null)
            txtVolume.text = "VOLUME: " + Mathf.RoundToInt(valor * 100f) + "%";
    }

    // Botao SAVE - grava volume + posicao do player (base para o seu sistema de save)
    public void Salvar()
    {
        PlayerPrefs.SetFloat("volume", AudioListener.volume);

        var player = FindAnyObjectByType<Controlapersonagem>();
        if (player != null)
        {
            Vector3 p = player.transform.position;
            PlayerPrefs.SetFloat("playerX", p.x);
            PlayerPrefs.SetFloat("playerY", p.y);
            PlayerPrefs.SetString("cena", SceneManager.GetActiveScene().name);
        }

        PlayerPrefs.Save();
        Feedback("Jogo salvo!");
    }

    // Botao EXIT - volta pro menu quando a cena existir no Build Settings
    public void Sair()
    {
        if (Application.CanStreamedLevelBeLoaded(cenaMenu))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(cenaMenu);
        }
        else
        {
            Feedback("A cena '" + cenaMenu + "' ainda nao existe. Crie a cena de menu e adicione em File > Build Settings.");
        }
    }

    private void Feedback(string msg)
    {
        if (txtFeedback != null) txtFeedback.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log("[MenuSistema] " + msg);
    }

    // Garante uma fonte mesmo se a fonte embutida do Unity mudar entre versoes
    private void AjustarFontes()
    {
        if (painel == null) return;

        Font fonte = null;
        try { fonte = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        if (fonte == null)
        {
            try { fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        }
        if (fonte == null) return;

        foreach (var t in painel.GetComponentsInChildren<Text>(true))
            if (t == null || t.font == null) { if (t != null) t.font = fonte; }
    }
}
