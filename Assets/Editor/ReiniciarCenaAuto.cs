using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// REINICIA A CENA COM SEGURANCA - salva as mudancas que estao no
// editor (objetos novos, roupas, etc.) ANTES de recarregar, e garante
// que o "Controle Cozinha" continue na cena.
//
//   1. salva a cena aberta (NADA do que voce mudou se perde);
//   2. garante o objeto "Controle Cozinha" (recria se a gravacao
//      tiver limpado a versao que estava no arquivo);
//   3. salva de novo e recarrega a cena do disco;
//   4. apaga o gatilho (roda UMA vez so).
//
// Auto: roda sozinho quando o Unity recompilar os scripts e achar o
//       arquivo "Temp/reiniciar_cena.trigger".
// Manual: menu "Restaura > Reiniciar Cena (salvar e recarregar)".
public static class ReiniciarCenaAuto
{
    private static string Gatilho
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "reiniciar_cena.trigger")); }
    }

    [InitializeOnLoadMethod]
    private static void AutoRodar()
    {
        if (!File.Exists(Gatilho)) return;

        // Da um tempinho apos a recarga dos scripts pra nao mexer na
        // cena no meio do carregamento do editor
        EditorApplication.delayCall += Reiniciar;
    }

    [MenuItem("Restaura/Reiniciar Cena (salvar e recarregar)")]
    public static void Reiniciar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Reiniciar] Estou no Play Mode - saia dele primeiro.");
            return;
        }

        try
        {
            var aberta = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string caminho = aberta.path;
            if (string.IsNullOrEmpty(caminho))
            {
                Debug.LogError("[Reiniciar] Cena sem caminho salvo - nada a fazer.");
                return;
            }

            // 1) SALVA primeiro: preserva objetos novos, roupas, tudo
            EditorSceneManager.SaveScene(aberta);
            Debug.Log("[Reiniciar] Cena salva (suas mudancas preservadas).");

            // 2) garante o Controle Cozinha (recria se faltar)
            ControleCozinha controle = Object.FindFirstObjectByType<ControleCozinha>();
            if (controle == null)
            {
                GameObject go = new GameObject("Controle Cozinha");
                controle = go.AddComponent<ControleCozinha>();
                Debug.Log("[Reiniciar] Objeto 'Controle Cozinha' criado na cena.");
            }
            controle.GarantirPratosPadrao(); // deixa os 10 pratos no Inspector

            // 3) salva com ele e recarrega do disco (cena limpa)
            EditorSceneManager.SaveScene(aberta);
            EditorSceneManager.OpenScene(caminho, OpenSceneMode.Single);

            // 4) concluiu: apaga o gatilho (roda so uma vez)
            if (File.Exists(Gatilho)) File.Delete(Gatilho);

            Debug.Log("[Reiniciar] Cena recarregada com tudo salvo. Pode dar Play!");
        }
        catch (System.Exception e)
        {
            // No passo 1 a cena ja foi salva - mesmo com erro, nada se
            // perde; o gatilho fica e tenta de novo na proxima compilacao
            Debug.LogError("[Reiniciar] Falhou (suas mudancas ja estavam salvas): " + e.Message);
        }
    }
}
