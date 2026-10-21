using UnityEngine;

/// <summary>Sonido continuo de una bola rodando: el volumen y el tono siguen a su velocidad
/// y solo suena mientras toca algo. Lo añade SonidosMaquina.</summary>
[RequireComponent(typeof(Rigidbody))]
public class RodadoSonoro : MonoBehaviour
{
    public AudioClip clip;
    public float volumenMax = 0.45f;
    [Tooltip("Velocidad (m/s) a la que el sonido alcanza su volumen máximo.")]
    public float velocidadReferencia = 1f;

    Rigidbody rb;
    AudioSource fuente;
    bool tocando, tocandoEstePaso;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.spatialBlend = 1f;
        fuente.minDistance = 0.3f;
        fuente.maxDistance = 12f;
        fuente.volume = 0f;
        fuente.Play();
    }

    void FixedUpdate()
    {
        tocando = tocandoEstePaso;
        tocandoEstePaso = false;
    }

    void OnCollisionStay(Collision choque) => tocandoEstePaso = true;

    void Update()
    {
        float v = rb.GetPointVelocity(rb.worldCenterOfMass).magnitude;
        float objetivo = tocando ? Mathf.Clamp01(v / velocidadReferencia) * volumenMax : 0f;
        fuente.volume = Mathf.MoveTowards(fuente.volume, objetivo, Time.deltaTime * 3f);
        fuente.pitch = 0.6f + Mathf.Clamp(v, 0f, 2f) * 0.5f;
    }
}
