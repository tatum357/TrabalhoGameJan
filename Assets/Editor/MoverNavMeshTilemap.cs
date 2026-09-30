using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// MOVE o objeto "NavMech2D" pra DENTRO de "tile map restaurante"
// (uso interno do time): deixa o NavMesh na parte do restaurante.
//
//   1. salva a cena aberta (nada do que voce mudou se perde);
//   2. reparenta o NavMech2D pro "tile map restaurante" mantendo a
//      posicao/rotacao no mundo (o bake nao muda);
//   3. salva de novo e apaga o gatilho (roda UMA vez so).
//
// Auto: roda sozinho quando o Unity recompilar os scripts e achar o
//       arquivo "Temp/mover_navmesh.trigger".
// Manual: menu "Restaura > Mover NavMech2D pra tile map restaurante".
public static class MoverNavMeshTilemap
{
    private static string Gatilho
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "mover_navmesh.trigger")); }
    }

    [InitializeOnLoadMethod]
    private static void AutoRodar()
    {
        if (!File.Exists(Gatilho)) return;

        // da um tempinho apos a recarga dos scripts pra nao mexer na
        // cena no meio do carregamento do editor
        EditorApplication.delayCall += Mover;
    }

    [MenuItem("Restaura/Mover NavMech2D pra tile map restaurante")]
    public static void Mover()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[MoverNavMesh] Estou no Play Mode - saia dele primeiro.");
            return;
        }

        try
        {
            Scene aberta = SceneManager.GetActiveScene();
            if (!aberta.IsValid() || string.IsNullOrEmpty(aberta.path))
            {
                Debug.LogError("[MoverNavMesh] Nenhuma cena salva aberta.");
                return;
            }

            // acha os dois objetos em qualquer nivel da hierarquia
            GameObject grid = null;
            GameObject nav = null;
            foreach (GameObject raiz in aberta.GetRootGameObjects())
            {
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "tile map restaurante") grid = t.gameObject;
                    if (t.name == "NavMech2D") nav = t.gameObject;
                }
            }

            if (grid == null || nav == null)
            {
                string faltando = grid == null ? "tile map restaurante" : "NavMech2D";
                Debug.LogError("[MoverNavMesh] Nao achei '" + faltando + "' na cena aberta. Abra a SampleScene e tente de novo.");
                return; // gatilho fica: tenta de novo na proxima compilacao
            }

            if (nav.transform.parent == grid.transform)
            {
                Debug.Log("[MoverNavMesh] NavMech2D ja esta dentro de 'tile map restaurante'.");
            }
            else
            {
                // 1) SALVA primeiro: preserva objetos novos, roupas, tudo
                EditorSceneManager.SaveScene(aberta);

                // reparenta mantendo posicao/rotacao no mundo (bake intacto)
                nav.transform.SetParent(grid.transform, true);

                // 2) salva com a mudanca
                EditorSceneManager.SaveScene(aberta);
                Debug.Log("[MoverNavMesh] NavMech2D movido pra dentro de 'tile map restaurante' e cena salva.");
            }

            if (File.Exists(Gatilho)) File.Delete(Gatilho);
        }
        catch (System.Exception e)
        {
            // no passo 1 a cena ja foi salva - mesmo com erro nada se
            // perde; o gatilho fica e tenta de novo na proxima compilacao
            Debug.LogError("[MoverNavMesh] Falhou (suas mudancas ja estavam salvas): " + e.Message);
        }
    }
}
