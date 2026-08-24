using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ControlaInimigo : MonoBehaviour
{
    public Transform alvo;
    public float velocidade = 5f;
    private SpriteRenderer Sp;

    [Header("Configurações de Delay")]
    public float intervaloAtualizacao = 1.0f;
    private float cronometro;
    private Vector3 direcaoAtual;
    private Animator animator;
    private Vector3 Escala = new Vector3(1, 1, 1);
    private Controlapersonagem Controlapersonagem;
    public bool TaAtacando = false;
    public float CronometroAtaque;
    public float CooldownAtaque;


    private void Start()
    {
        Controlapersonagem = FindAnyObjectByType<Controlapersonagem>();
        Sp = this.GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (alvo == null) return;

        if (CronometroAtaque > 0)
        {
            CronometroAtaque -= Time.deltaTime;
        }

        cronometro -= Time.deltaTime;
        if (cronometro <= 0)
        {
            Vector3 deslocamento = alvo.position - transform.position;
            direcaoAtual = deslocamento.normalized;
            cronometro = intervaloAtualizacao;
        }

        if (TaAtacando == false)
        {
            transform.Translate(direcaoAtual * velocidade * Time.deltaTime, Space.World);
            if (!animator.GetBool("1_Move")) animator.SetBool("1_Move", true);
        }
        else
        {
            if (animator.GetBool("1_Move")) animator.SetBool("1_Move", false);
        }

        float direcaoRelativa = alvo.position.x - transform.position.x;
        float lado = (direcaoRelativa > 0) ? -1 : 1;
        transform.localScale = new Vector3(lado, 1, 1);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player") && CronometroAtaque <= 0)
        {
            CronometroAtaque = (CooldownAtaque * 0) + CooldownAtaque;
            TaAtacando = true;
            StartCoroutine(AtaqueInimigo());

            var scriptPlayer = other.GetComponentInParent<Controlapersonagem>();
            if (scriptPlayer != null)
            {
                scriptPlayer.StartCoroutine("FoiAcertado");
            }
        }
    }

    private IEnumerator AtaqueInimigo()
    {
        TaAtacando = false;
        animator.SetTrigger("AxelAttack_1");
       yield return new WaitForSeconds(0.5f);

    }
}
