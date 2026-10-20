using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cámara que sigue la acción de la máquina de Rube Goldberg.
/// Ponla en la Main Camera. Tecla C: alterna entre seguimiento automático y vista general.
///
///  - Antes de arrancar: vista general de toda la máquina.
///  - Bola 1 bajando por la rampa.
///  - El frente de la cadena de dominós (el primer dominó que sigue en pie).
///  - Bola 2 por el carril, la espiral, la campana y la meta.
///  - Celebración: primer plano de la meta (bandera, polea y confeti).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CamaraSeguimiento : MonoBehaviour
{
    public MaquinaRubeGoldberg maquina;
    public bool automatica = true;

    [Header("Encuadre")]
    public float suavizado = 0.45f;
    public float distanciaGeneral = 3.0f;
    public float alturaGeneral = 1.5f;
    public float distanciaCerca = 0.9f;
    public float alturaCerca = 0.4f;
    [Tooltip("Cuánto se desplaza la cámara hacia la derecha respecto a la dirección frontal.")]
    public float lateral = 0.45f;

    Transform bola1, bola2, pestillo, meta;
    Transform[] dominos;
    Vector3 frente, derecha, centroGeneral;
    Vector3 velocidad, mirarActual;

    void Start()
    {
        if (maquina == null)
        {
#if UNITY_2023_1_OR_NEWER
            maquina = FindFirstObjectByType<MaquinaRubeGoldberg>();
#else
            maquina = FindObjectOfType<MaquinaRubeGoldberg>();
#endif
        }
        if (maquina == null) { Debug.LogError("CamaraSeguimiento: no encuentro MaquinaRubeGoldberg."); enabled = false; return; }

        var raiz = maquina.transform;
        bola1 = Buscar(raiz, "Bola_1_Rampa");
        bola2 = Buscar(raiz, "Bola_2_Carril");
        pestillo = Buscar(raiz, "Mec_Pestillo");
        meta = Buscar(raiz, "Meta_Trigger");
        dominos = new Transform[23];
        for (int i = 0; i < 23; i++) dominos[i] = Buscar(raiz, $"Domino_{i + 1:00}");

        // Direcciones sacadas de la propia escena: "frente" = de los engranajes hacia la fila de dominós,
        // "derecha" = hacia la espiral. Así no depende de cómo Unity convierta los ejes del FBX.
        Vector3 f = dominos[14].position - pestillo.position; f.y = 0f; frente = f.normalized;
        Vector3 r = Buscar(raiz, "Pista_Espiral").position - Buscar(raiz, "Plataforma_Losa").position;
        r.y = 0f; r -= Vector3.Project(r, frente); derecha = r.normalized;

        // Centro de la máquina (sin las paredes del taller).
        var caja = new Bounds(pestillo.position, Vector3.zero);
        foreach (var rend in raiz.GetComponentsInChildren<Renderer>())
            if (!rend.name.StartsWith("Taller_")) caja.Encapsulate(rend.bounds);
        centroGeneral = caja.center;

        Elegir(out Vector3 mirar, out float d, out float h);
        mirarActual = mirar;
        transform.position = Posicion(mirar, d, h);
        transform.rotation = Quaternion.LookRotation(mirar - transform.position, Vector3.up);
    }

    void LateUpdate()
    {
        if (TeclaC()) automatica = !automatica;

        Elegir(out Vector3 mirar, out float dist, out float alt);
        transform.position = Vector3.SmoothDamp(transform.position, Posicion(mirar, dist, alt), ref velocidad, suavizado);
        mirarActual = Vector3.Lerp(mirarActual, mirar, 1f - Mathf.Exp(-5f * Time.deltaTime));
        transform.rotation = Quaternion.LookRotation(mirarActual - transform.position, Vector3.up);
    }

    Vector3 Posicion(Vector3 mirar, float dist, float alt) =>
        mirar + frente * dist + derecha * dist * lateral + Vector3.up * alt;

    void Elegir(out Vector3 mirar, out float dist, out float alt)
    {
        if (!automatica || !maquina.Arrancado)
        {
            mirar = centroGeneral; dist = distanciaGeneral; alt = alturaGeneral; return;
        }
        dist = distanciaCerca; alt = alturaCerca;
        if (maquina.Celebrando) { mirar = meta.position + Vector3.up * 0.15f; return; }

        int enPie = -1;
        for (int i = 0; i < dominos.Length; i++)
            if (dominos[i].up.y > 0.6f) { enPie = i; break; }

        if (enPie == 0) mirar = bola1.position;                 // la bola 1 aún no ha llegado al primer dominó
        else if (enPie > 0) mirar = dominos[enPie].position;    // frente de la cadena
        else mirar = bola2.position;                             // todos caídos: seguir a la bola 2
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        Debug.LogError($"CamaraSeguimiento: no encuentro '{nombre}'.");
        return raiz;
    }

    static bool TeclaC()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.C)) return true;
#endif
        return false;
    }
}
