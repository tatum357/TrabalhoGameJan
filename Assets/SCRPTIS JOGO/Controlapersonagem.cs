using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Controlapersonagem : MonoBehaviour
{
    private Animator Anim;
    [SerializeField] private float Velocidade = 5f;
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
    private ControlaInimigo controlaInimigo;

    void Start()
    {
        Anim = GetComponent<Animator>();
        Bc = GetComponent<BoxCollider2D>();
        if (Tran == null) Tran = transform;
        // Unity 2022: FindFirstObjectByType e o metodo recomendado
        controlaInimigo = FindFirstObjectByType<ControlaInimigo>();
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
        bool emMovimento = moveX != 0 || moveY != 0;
        if (moveX != 0)
        {
            Escala = new Vector3(2.5F * -Mathf.Sign(moveX), 2.5F, 1);
        }
        else
        {
            Escala = new Vector3(2.5F, 2.5F, 1);
        }
        if (Anim != null) Anim.SetBool("1_Move", emMovimento);
        PtaParado = !emMovimento;
        if (Tran != null) Tran.localScale = Escala;
        Vector3 direcao = new Vector3(moveX, moveY, 0f).normalized;
        transform.Translate(direcao * Velocidade * Time.deltaTime, Space.World);
    }

    public IEnumerator FoiAcertado()
    {
        if (Bc != null) Bc.enabled = false;
        if (Anim != null) Anim.SetTrigger("3_Damaged");
        yield return new WaitForSeconds(0.4f);
        if (Bc != null) Bc.enabled = true;
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
        float velocidadeOriginal = Velocidade;
        Velocidade = 1; 
        Anim.SetTrigger("2_Attack");
        yield return new WaitForSeconds(0.5f);
        Velocidade = velocidadeOriginal;
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    // FoiAcertado ativo movido para fora do bloco comentado (versao com null-checks).
    /*public IEnumerator FoiAcertado_Legado()
    {
        Bc.enabled = false;
        Vidas--;
        Anim.SetTrigger("3_Damaged");
        yield return new WaitForSeconds(0.4f);
        Bc.enabled = true;
    }*/
}
