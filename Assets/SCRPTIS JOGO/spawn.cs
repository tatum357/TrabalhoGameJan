using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawn : MonoBehaviour
{
   public GameObject cliente;
   public float spawner;

   private float proximo = 0f;
    void Update()
    {
        if (spawner <= 0f) return;
        if (Time.time > proximo)
        {
            proximo = Time.time + spawner;

            if (cliente != null)
                Instantiate(cliente, transform.position, cliente.transform.rotation);
        }
    }
}
