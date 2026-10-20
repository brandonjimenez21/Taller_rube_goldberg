using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_6000_0_OR_NEWER
using MaterialFisico = UnityEngine.PhysicsMaterial;
using CombinarFisico = UnityEngine.PhysicsMaterialCombine;
#else
using MaterialFisico = UnityEngine.PhysicMaterial;
using CombinarFisico = UnityEngine.PhysicMaterialCombine;
#endif

/// <summary>
/// Controla la máquina de Rube Goldberg importada desde RubeGoldberg_Unity.fbx.
/// Colócalo en el objeto raíz "RubeGoldberg".
///
/// Secuencia:
///  1. Arranque: la rueda grande (manivela) mueve el engranaje intermedio y éste el del pestillo.
///     En el eje del engranaje pequeño va la palanca: la Ficha_Pestillo retiene la Bola 1 y la
///     Ficha_Contrapeso la equilibra. La palanca se levanta y la Bola 1 baja por la rampa.
///  2. Cadena física: Bola 1 -> 23 dominós -> Bola 2 -> carril -> espiral -> campana -> meta.
///  3. Meta: la bola cae sobre la plataforma de la polea; la plataforma baja con su peso y, por la
///     cuerda que pasa por la polea del mástil, sube la bandera. Los engranajes giran, suena una
///     fanfarria y sale confeti.
/// </summary>
public class MaquinaRubeGoldberg : MonoBehaviour
{
    [Header("Piezas (si se dejan vacías se buscan por nombre)")]
    public Transform ruedaGrande;          // Mec_Rueda_Grande (lleva la Mec_Manivela como hija)
    public Transform engranajeIntermedio;  // Mec_Engranaje_Intermedio
    public Transform engranajePestillo;    // Mec_Engranaje_Pestillo
    public Transform pestillo;             // Mec_Pestillo (palanca, hija del engranaje del pestillo)
    public Transform fichaPestillo;        // Mec_Ficha_Pestillo
    public Transform bola1;                // Bola_1_Rampa
    public Transform banderaTela;          // Meta_Bandera_Tela
    public Transform metaTrigger;          // Meta_Trigger
    public Transform campana;              // Campana
    public Transform campanaBisagra;       // Campana_Bisagra (punto de giro)
    public Transform campanaEje;           // Campana_Eje (barra del pórtico)
    public Transform poleaRueda;           // Polea_Rueda
    public Transform poleaPlataforma;      // Polea_Plataforma

    [Header("Dientes de cada engranaje")]
    public int dientesRueda = 14;
    public int dientesIntermedio = 12;
    public int dientesPestillo = 8;

    [Header("Arranque")]
    [Tooltip("Grados que gira la palanca del pestillo para liberar la bola.")]
    public float aperturaPestillo = 120f;
    public float duracionArranque = 1.5f;
    public bool arrancarConEspacio = true;

    [Header("Meta y celebración")]
    [Tooltip("Grados por segundo de la rueda grande durante la celebración.")]
    public float velocidadCelebracion = 120f;
    [Tooltip("Metros que baja la plataforma (y sube la bandera).")]
    public float recorridoPolea = 0.285f;
    public float radioPolea = 0.0486f;
    public float duracionPolea = 2f;
    public int cantidadConfeti = 300;

    [Header("Física")]
    public bool configurarFisica = true;
    [Tooltip("Paso fijo de la física. Con la máquina a escala real conviene 0.0025 s.")]
    public float pasoFisica = 0.0025f;
    [Tooltip("Fricción de rampas, pistas y bolas.")]
    public float friccion = 0.3f;
    [Tooltip("Fricción de los dominós (se usa el mínimo con la superficie). Con valores altos se atascan en la curva.")]
    public float friccionDomino = 0.15f;
    public float masaDomino = 0.5f;
    public float masaBola = 2f;
    public float masaCampana = 0.25f;

    public bool Arrancado => arrancado;
    public bool Celebrando => celebrando;

    static readonly string[] prefijosEstaticos =
    {
        "Suelo", "Taller_", "Plataforma_", "Carril_", "Pata_", "Baranda_", "Pista_Espiral", "Rampa_Salida",
        "Soporte_", "Hueco_Meta", "Meta_Tope", "Meta_Pared", "Meta_Bandera_Mastil", "Mec_Rampa_Bajada",
        "Mec_Base_Bloques", "Mec_Decor", "Campana_Portico", "Campana_Eje"
    };

    float angulo;          // ángulo de la rueda grande, en grados
    float sentido = 1f;    // se calibra en Start para que el pestillo se aleje de la bola
    Vector3 eje;           // eje común de los engranajes (en mundo)
    Quaternion rot1, rot2, rot3, rotPolea;
    int ultimoDiente;
    bool arrancado, celebrando;

    // polea
    Vector3 banderaInicio, plataformaInicio, ejePolea;
    float sentidoPolea = 1f;
    Rigidbody rbPlataforma;
    LineRenderer cuerda;

    SonidosMaquina sonidos;
    readonly List<(Rigidbody rb, Vector3 pos, Quaternion rot)> estadoInicial = new List<(Rigidbody, Vector3, Quaternion)>();

    void Awake()
    {
        ruedaGrande = ruedaGrande ? ruedaGrande : Buscar("Mec_Rueda_Grande");
        engranajeIntermedio = engranajeIntermedio ? engranajeIntermedio : Buscar("Mec_Engranaje_Intermedio");
        engranajePestillo = engranajePestillo ? engranajePestillo : Buscar("Mec_Engranaje_Pestillo");
        pestillo = pestillo ? pestillo : Buscar("Mec_Pestillo");
        fichaPestillo = fichaPestillo ? fichaPestillo : Buscar("Mec_Ficha_Pestillo");
        bola1 = bola1 ? bola1 : Buscar("Bola_1_Rampa");
        banderaTela = banderaTela ? banderaTela : Buscar("Meta_Bandera_Tela");
        metaTrigger = metaTrigger ? metaTrigger : Buscar("Meta_Trigger");
        campana = campana ? campana : Buscar("Campana");
        campanaBisagra = campanaBisagra ? campanaBisagra : Buscar("Campana_Bisagra");
        campanaEje = campanaEje ? campanaEje : Buscar("Campana_Eje");
        poleaRueda = poleaRueda ? poleaRueda : Buscar("Polea_Rueda");
        poleaPlataforma = poleaPlataforma ? poleaPlataforma : Buscar("Polea_Plataforma");

        if (configurarFisica) ConfigurarFisica();
        ConfigurarCampana();
        ConfigurarMeta();

        sonidos = Asegurar<SonidosMaquina>(this);
        sonidos.Configurar(transform);
    }

    void Start()
    {
        // El eje del engranaje del pestillo pasa por la palanca: de ahí sale el eje común.
        eje = (pestillo.position - engranajePestillo.position).normalized;
        rot1 = ruedaGrande.rotation;
        rot2 = engranajeIntermedio.rotation;
        rot3 = engranajePestillo.rotation;

        // Calibrar el sentido de giro: el pestillo debe alejarse de la bola al abrir.
        float d0 = Vector3.Distance(fichaPestillo.position, bola1.position);
        engranajePestillo.rotation = Quaternion.AngleAxis(10f, eje) * rot3;
        float d1 = Vector3.Distance(fichaPestillo.position, bola1.position);
        engranajePestillo.rotation = rot3;
        sentido = d1 >= d0 ? 1f : -1f;

        PrepararPolea();

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
            if (!rb.isKinematic) estadoInicial.Add((rb, rb.position, rb.rotation));
    }

    void Update()
    {
        if (!arrancado && arrancarConEspacio && EspacioPresionado()) Arrancar();
    }

    void FixedUpdate()
    {
        if (celebrando) angulo += velocidadCelebracion * Time.fixedDeltaTime;
        AplicarAngulo(angulo);
    }

    void LateUpdate() => ActualizarCuerda();

    // ---------------------------------------------------------------- engranajes

    void AplicarAngulo(float a)
    {
        float r12 = (float)dientesRueda / dientesIntermedio;
        float r13 = (float)dientesRueda / dientesPestillo;
        ruedaGrande.rotation = Quaternion.AngleAxis(sentido * a, eje) * rot1;
        engranajeIntermedio.rotation = Quaternion.AngleAxis(-sentido * a * r12, eje) * rot2;   // gira al revés
        engranajePestillo.rotation = Quaternion.AngleAxis(sentido * a * r13, eje) * rot3;      // lleva la palanca

        int diente = Mathf.FloorToInt(a / (360f / dientesRueda));
        if (diente != ultimoDiente)
        {
            ultimoDiente = diente;
            if (sonidos != null) sonidos.Tick(ruedaGrande.position);
        }
    }

    public void Arrancar()
    {
        if (arrancado) return;
        arrancado = true;
        StartCoroutine(RutinaArranque());
    }

    IEnumerator RutinaArranque()
    {
        // La rueda grande gira lo justo para que el pestillo abra 'aperturaPestillo' grados.
        float objetivo = aperturaPestillo * dientesPestillo / (float)dientesRueda;
        float t = 0f;
        while (t < duracionArranque)
        {
            t += Time.fixedDeltaTime;
            angulo = objetivo * Mathf.SmoothStep(0f, 1f, t / duracionArranque);
            yield return new WaitForFixedUpdate();
        }
        angulo = objetivo;
    }

    // ---------------------------------------------------------------- campana

    void ConfigurarCampana()
    {
        if (campana == null || campanaBisagra == null || campanaEje == null) return;
        var rb = Asegurar<Rigidbody>(campana);
        rb.mass = masaCampana;
#if UNITY_6000_0_OR_NEWER
        rb.angularDamping = 1.5f;
#else
        rb.angularDrag = 0.8f;
#endif
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.sleepThreshold = 0f;   // un péndulo lento no debe "dormirse" desviado de la vertical
        var mc = Asegurar<MeshCollider>(campana);
        mc.convex = true;

        // La barra del pórtico pasa por el "ojal" de la campana, pero su collider convexo no tiene hueco:
        // sin esto choca con la barra al empezar y se queda desviada ~90°.
        var colBarra = campanaEje.GetComponent<Collider>();
        if (colBarra != null) Physics.IgnoreCollision(mc, colBarra);

        // El eje de giro es la barra del pórtico: su dimensión más larga.
        var ext = campanaEje.GetComponent<Renderer>().bounds.extents;
        Vector3 ejeMundo = ext.x >= ext.y && ext.x >= ext.z ? Vector3.right : (ext.y >= ext.z ? Vector3.up : Vector3.forward);

        var bisagra = Asegurar<HingeJoint>(campana);
        bisagra.connectedBody = null;   // anclada al mundo
        bisagra.autoConfigureConnectedAnchor = true;
        bisagra.anchor = campana.InverseTransformPoint(campanaBisagra.position);
        bisagra.axis = campana.InverseTransformDirection(ejeMundo);

        // Que se balancee como un péndulo sin dar vueltas completas alrededor de la barra.
        bisagra.useLimits = true;
        bisagra.limits = new JointLimits { min = -110f, max = 110f, bounciness = 0.3f };
    }

    // ---------------------------------------------------------------- polea y meta

    void PrepararPolea()
    {
        banderaInicio = banderaTela.position;
        plataformaInicio = poleaPlataforma.position;
        rotPolea = poleaRueda.rotation;
        rbPlataforma = Asegurar<Rigidbody>(poleaPlataforma);
        rbPlataforma.isKinematic = true;
        rbPlataforma.useGravity = false;
        if (poleaPlataforma.GetComponent<Collider>() == null) poleaPlataforma.gameObject.AddComponent<BoxCollider>();

        // Eje de la polea: perpendicular al plano que forman los dos tramos de cuerda.
        PuntosCuerda(out _, out Vector3 llantaPlataforma, out _, out Vector3 llantaBandera, out _);
        ejePolea = Vector3.Cross(llantaBandera - llantaPlataforma, Vector3.up).normalized;
        Vector3 vel = Vector3.Cross(ejePolea, llantaBandera - poleaRueda.position);
        sentidoPolea = vel.y > 0f ? 1f : -1f;   // al girar en este sentido, el lado de la bandera sube

        var go = new GameObject("Cuerda_Polea");
        go.transform.SetParent(transform, false);
        cuerda = go.AddComponent<LineRenderer>();
        cuerda.positionCount = 5;
        cuerda.widthMultiplier = 0.004f;
        cuerda.numCapVertices = 2;
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) cuerda.material = new Material(shader);
        cuerda.startColor = cuerda.endColor = new Color(0.55f, 0.42f, 0.25f);
        ActualizarCuerda();
    }

    void PuntosCuerda(out Vector3 enPlataforma, out Vector3 llantaPlataforma, out Vector3 arriba,
                      out Vector3 llantaBandera, out Vector3 enBandera)
    {
        Vector3 c = poleaRueda.position;
        enBandera = banderaTela.position + Vector3.up * 0.06f;           // esquina superior junto al mástil
        llantaBandera = new Vector3(enBandera.x, c.y, enBandera.z);
        llantaPlataforma = 2f * c - llantaBandera; llantaPlataforma.y = c.y;
        arriba = c + Vector3.up * radioPolea;
        enPlataforma = new Vector3(llantaPlataforma.x, poleaPlataforma.position.y + 0.03f, llantaPlataforma.z);
    }

    void ActualizarCuerda()
    {
        if (cuerda == null) return;
        PuntosCuerda(out var a, out var b, out var c, out var d, out var e);
        cuerda.SetPosition(0, a); cuerda.SetPosition(1, b); cuerda.SetPosition(2, c);
        cuerda.SetPosition(3, d); cuerda.SetPosition(4, e);
    }

    IEnumerator RutinaPolea()
    {
        // La bola pesa sobre la plataforma: baja, y por la cuerda sube la bandera lo mismo.
        for (float t = 0f; t < duracionPolea; t += Time.fixedDeltaTime)
        {
            AplicarPolea(recorridoPolea * Mathf.SmoothStep(0f, 1f, t / duracionPolea));
            yield return new WaitForFixedUpdate();
        }
        AplicarPolea(recorridoPolea);
    }

    void AplicarPolea(float d)
    {
        rbPlataforma.MovePosition(plataformaInicio - Vector3.up * d);
        banderaTela.position = banderaInicio + Vector3.up * d;
        poleaRueda.rotation = Quaternion.AngleAxis(sentidoPolea * d / radioPolea * Mathf.Rad2Deg, ejePolea) * rotPolea;
    }

    public void Celebrar()
    {
        if (celebrando) return;
        celebrando = true;
        StartCoroutine(RutinaPolea());
        LanzarConfeti();
        if (sonidos != null) sonidos.Fanfarria();
    }

    void LanzarConfeti()
    {
        var go = new GameObject("Confeti");
        go.transform.position = metaTrigger.position + Vector3.up * 0.36f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // el cono emite hacia arriba
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.duration = 1f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.35f;
        main.maxParticles = cantidadConfeti;
        var colores = new Gradient();
        colores.SetKeys(
            new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 0.25f),
                    new GradientColorKey(Color.green, 0.5f), new GradientColorKey(Color.cyan, 0.75f),
                    new GradientColorKey(Color.magenta, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(colores) { mode = ParticleSystemGradientMode.RandomColor };

        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cantidadConfeti) });

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 35f;
        forma.radius = 0.1f;

        var giro = ps.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null) go.GetComponent<ParticleSystemRenderer>().material = new Material(shader);

        ps.Play();
        Destroy(go, 6f);
    }

    // ---------------------------------------------------------------- reinicio

    public void Reiniciar()
    {
        StopAllCoroutines();
        arrancado = false;
        celebrando = false;
        angulo = 0f;
        AplicarAngulo(0f);
        rbPlataforma.position = plataformaInicio;
        poleaPlataforma.position = plataformaInicio;
        banderaTela.position = banderaInicio;
        poleaRueda.rotation = rotPolea;
        foreach (var (rb, pos, rot) in estadoInicial)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
            rb.transform.SetPositionAndRotation(pos, rot);
        }
    }

    void OnGUI()
    {
        var boton = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        if (!arrancado)
        {
            if (GUI.Button(new Rect(20, 20, 300, 50), "Iniciar máquina (Espacio)", boton)) Arrancar();
        }
        else if (GUI.Button(new Rect(20, 20, 160, 50), "Reiniciar", boton))
        {
            Reiniciar();
        }

        if (celebrando)
        {
            var titulo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            titulo.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, 40, Screen.width, 100), "¡META!", titulo);
        }
    }

    // ---------------------------------------------------------------- configuración

    void ConfigurarFisica()
    {
        Time.fixedDeltaTime = pasoFisica;
        Physics.defaultSolverIterations = 20;
        Physics.defaultSolverVelocityIterations = 8;

        var material = new MaterialFisico("RubeGoldberg")
        {
            dynamicFriction = friccion,
            staticFriction = friccion,
            bounciness = 0.1f,
            frictionCombine = CombinarFisico.Average,
            bounceCombine = CombinarFisico.Average
        };
        // Dominós: poca fricción y "Minimum" para que mande sobre la de la mesa y la de otros dominós.
        var materialDomino = new MaterialFisico("RubeGoldberg_Domino")
        {
            dynamicFriction = friccionDomino,
            staticFriction = friccionDomino,
            bounciness = 0.05f,
            frictionCombine = CombinarFisico.Minimum,
            bounceCombine = CombinarFisico.Average
        };

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<MeshFilter>() == null) continue;
            string n = t.name;

            if (n.StartsWith("Domino_"))
            {
                Asegurar<BoxCollider>(t).sharedMaterial = materialDomino;
                CrearCuerpo(t, masaDomino, false);
            }
            else if (n.StartsWith("Bola_"))
            {
                Asegurar<SphereCollider>(t).sharedMaterial = material;
                CrearCuerpo(t, masaBola, true);
            }
            else if (pestillo != null && t.IsChildOf(pestillo))
            {
                var mc = Asegurar<MeshCollider>(t);
                mc.convex = true;
                mc.sharedMaterial = material;
            }
            else if (EsEstatico(n))
            {
                var mc = Asegurar<MeshCollider>(t);
                mc.convex = false;
                mc.sharedMaterial = material;
            }
        }

        // La palanca del pestillo se mueve con el engranaje: cuerpo cinemático.
        if (pestillo != null)
        {
            var rb = Asegurar<Rigidbody>(pestillo);
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void CrearCuerpo(Transform t, float masa, bool esBola)
    {
        var rb = Asegurar<Rigidbody>(t);
        rb.mass = masa;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = 0.04f;
        rb.angularDamping = 0.1f;
#else
        rb.drag = 0.04f;
        rb.angularDrag = 0.1f;
#endif
        if (esBola)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0f;   // que no se "duerma" esperando al pestillo
        }
    }

    void ConfigurarMeta()
    {
        if (metaTrigger == null) return;
        var caja = Asegurar<BoxCollider>(metaTrigger);
        caja.isTrigger = true;
        caja.size = new Vector3(0.27f, 0.15f, 0.27f);   // justo encima de la plataforma de la polea
        Asegurar<MetaTrigger>(metaTrigger).maquina = this;
    }

    static bool EsEstatico(string n)
    {
        foreach (var p in prefijosEstaticos)
            if (n.StartsWith(p)) return true;
        return false;
    }

    static T Asegurar<T>(Component c) where T : Component
    {
        var x = c.GetComponent<T>();
        return x != null ? x : c.gameObject.AddComponent<T>();
    }

    Transform Buscar(string nombre)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        Debug.LogError($"MaquinaRubeGoldberg: no encuentro '{nombre}' dentro de {name}.");
        return null;
    }

    static bool EspacioPresionado()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space)) return true;
#endif
        return false;
    }
}
