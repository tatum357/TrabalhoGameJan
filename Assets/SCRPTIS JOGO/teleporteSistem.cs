using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SistemaControl : MonoBehaviour
{
   [SerializeField] private Transform PosicaoPortaCozinha;
   [SerializeField] private Transform PosicaoPortaRestaurante;
    
    public GameObject Player;

    public void Teleporte(GameObject LocalDeIda)
    {
        
        if (LocalDeIda.CompareTag("PortaCozinha") || (LocalDeIda.CompareTag("PortaRestaurante")))
        {
            Player.transform.position = LocalDeIda.transform.position; //+ ValorAdicional;
        }
    }
}
