using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Controlapersonagem : MonoBehaviour
{
    private Animator Anim;
    [SerializeField] private int Velocidade = 0;
    [SerializeField] private Rigidbody2D rb;
    public Transform Tran;
    private Vector3 Escala = new Vector3(1,1,1);
    //private int DanoDoAtaque;
    //private int minDamege = 1;
    //private int maxDamege = 10;
    public float attackCooldown = 0.3f;
    //private bool canAttack = true;
    public float moveX;
    public bool PtaParado = true;
    private BoxCollider2D Bc;
    //private int Vidas = 3;
    private ControlaInimigo ControlaInimigo;
    private SistemaControl Teleporte;

    void Start()
    {
        Anim = GetComponent<Animator>();
        Bc = GetComponent<BoxCollider2D>();
        ControlaInimigo = FindAnyObjectByType<ControlaInimigo>();
        Teleporte = FindAnyObjectByType<SistemaControl>();
    }

    // Update is called once per frame
    void Update()
    {
        Movimento();
        /*if ((Input.GetMouseButtonDown(0)) && (canAttack == true))
        {
            Atacar();
        }*/
    }
    /*private void Atacar()
    {
       // EnemyHealth enemy = other.GetComponent<EnemyHealth>();
        float damageDealt = Random.Range(minDamege, maxDamege);
        /*if (enemy != null)
        {
            enemy.TakeDamage(damageDealt);
        }
        StartCoroutine("Ataque");
    }*/

    private void Movimento()
    {
        moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        if (moveX != 0)
        {
            Escala = new Vector3(2.5F * -moveX, 2.5F, 3);
            Anim.SetBool("1_Move", true);
            PtaParado = false;
        }
        else
        {
            Escala = new Vector3(2.5F, 2.5F, 3);
            Anim.SetBool("1_Move", false);
            PtaParado = true;
        }
        Tran.localScale = Escala;
        Vector3 direcao = new Vector3(moveX, moveY, 0f).normalized;
        transform.Translate(direcao * Velocidade * Time.deltaTime, Space.World);
    }

    /*private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("ArmaInimigo"))
        {
            ControlaInimigo inimigoAgressor = other.GetComponentInParent<ControlaInimigo>();

            if (inimigoAgressor != null && inimigoAgressor.TaAtacando)
            {
                StartCoroutine("FoiAcertado");
                Debug.Log("O player foi atingido por: " + other.name);
            }
        }
    }

    private IEnumerator Ataque()
    {
        canAttack = false;
        int velocidadeOriginal = Velocidade;
        Velocidade = 1; 
        Anim.SetTrigger("2_Attack");
        yield return new WaitForSeconds(0.5f);
        Velocidade = velocidadeOriginal;
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    public IEnumerator FoiAcertado()
    {
        Bc.enabled = false;
        Vidas--;
        Anim.SetTrigger("3_Damaged");
        yield return new WaitForSeconds(0.4f);
        Bc.enabled = true;
    }*/
}
