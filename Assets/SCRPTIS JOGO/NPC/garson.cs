using System.Collections;
using UnityEngine;

public class garson : MonoBehaviour
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
        StartCoroutine(setpatrolpoint());
    }


    void Update()
    {
        if(inpause)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        Vector2 Direction = ((Vector3)target - transform.position).normalized;
        if(Direction.x < 0 && transform.localScale.x > 0 || Direction.x > 0 && transform.localScale.x < 0)
            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
        rb.velocity = Direction * velocidade;
        if (Vector2.Distance(transform.position, target) < 1f)
            StartCoroutine(setpatrolpoint());
    }
    IEnumerator setpatrolpoint()
    {
        inpause = true;
        anim.Play("idle");
        yield return new WaitForSeconds(pausa);
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolpoint.Length;
        target = patrolpoint[currentPatrolIndex];
        inpause = false;
        anim.Play("Walk");
    }

}
