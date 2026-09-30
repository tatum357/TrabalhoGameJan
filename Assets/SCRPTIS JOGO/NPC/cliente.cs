using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cliente : MonoBehaviour
{
    public Vector2[] patrolpoint;
    private int currentPatrolIndex;
    private Vector2 target;
    public float pausa = 1.5f;
    private bool inpause;
    public float velocidade = 2;

    private Rigidbody2D rb;
    private Animator anim;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        if (patrolpoint != null && patrolpoint.Length > 0)
        {
            currentPatrolIndex = 0;
            target = patrolpoint[0];
            StartCoroutine(setpatrolpoint());
        }
        else
        {
            Debug.LogWarning("garson: patrolpoint vazio.", this);
        }
    }


    void Update()
    {
        if (rb == null) return;
        if (patrolpoint == null || patrolpoint.Length == 0) return;
        if (inpause)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        Vector2 direction = ((Vector3)target - transform.position).normalized;
        if (direction.x < 0 && transform.localScale.x > 0 || direction.x > 0 && transform.localScale.x < 0)
            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
        rb.velocity = direction * velocidade;
        if (Vector2.Distance(transform.position, target) < 1f && !inpause)
            StartCoroutine(setpatrolpoint());
    }
    IEnumerator setpatrolpoint()
    {
        inpause = true;
        if (rb != null) rb.velocity = Vector2.zero;
        if (anim != null) anim.Play("idle");
        yield return new WaitForSeconds(pausa);
        if (patrolpoint == null || patrolpoint.Length == 0) { inpause = false; yield break; }
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolpoint.Length;
        target = patrolpoint[currentPatrolIndex];
        inpause = false;
        if (anim != null) anim.Play("Walk");
    }
}
