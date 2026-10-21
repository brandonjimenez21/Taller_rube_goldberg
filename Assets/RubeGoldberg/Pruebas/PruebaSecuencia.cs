using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Banco de pruebas temporal: ejecuta la secuencia completa varias veces (con perturbaciones aleatorias)
/// y deja un resumen por prueba en PruebaSecuencia.Registro.</summary>
public class PruebaSecuencia : MonoBehaviour
{
    public static readonly List<string> Registro = new List<string>();
    public static bool Terminado;
    public static int Oks, Total;
    static void Log(string s) { Registro.Add(s); Debug.Log(s); }

    public enum Modo { Completo, SoloBola2 }

    public float tiempoMaximo = 30f;
    public float capturaDelta = 0.01f;
    public int repeticiones = 10;
    public float jitterPosicion = 0.004f;   // m, perturbación de la posición inicial de la Bola 1
    public float jitterFriccion = 0.05f;
    public float jitterMasaBola = 0.3f;
    public Modo modo = Modo.Completo;
    public bool detalle;

    MaquinaRubeGoldberg m;
    Transform bola1, bola2, bandera, plataforma, campana;
    Transform[] dominos = new Transform[23];
    Rigidbody rb1, rb2;
    PhysicsMaterial matDomino, matGeneral;
    Quaternion campanaRot0, campanaRotW0;
    Vector3 bandera0, plataforma0;
    float fricDom0, fricGen0, masaBola0; int zoomN;

    static Transform Buscar(Transform r, string n)
    {
        foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    IEnumerator Start()
    {
        Registro.Clear(); Terminado = false; Oks = Total = 0;
        Application.logMessageReceived += (c, st, tipo) =>
        {
            if (tipo != LogType.Log && !c.StartsWith("[PRUEBA]") && !c.Contains("NoSubscription")) Registro.Add($"[CONSOLA-{tipo}] {c}");
        };
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Time.captureDeltaTime = capturaDelta;

        m = FindFirstObjectByType<MaquinaRubeGoldberg>();
        var r = m.transform;
        bola1 = Buscar(r, "Bola_1_Rampa"); bola2 = Buscar(r, "Bola_2_Carril");
        bandera = Buscar(r, "Meta_Bandera_Tela"); plataforma = Buscar(r, "Polea_Plataforma"); campana = Buscar(r, "Campana");
        for (int i = 0; i < 23; i++) dominos[i] = Buscar(r, $"Domino_{i + 1:00}");
        rb1 = bola1.GetComponent<Rigidbody>(); rb2 = bola2.GetComponent<Rigidbody>();
        matDomino = dominos[0].GetComponent<Collider>().sharedMaterial;
        matGeneral = bola1.GetComponent<Collider>().sharedMaterial;
        fricDom0 = matDomino.dynamicFriction; fricGen0 = matGeneral.dynamicFriction; masaBola0 = rb1.mass;
        yield return new WaitForSeconds(0.5f);
        campanaRot0 = campana.localRotation; campanaRotW0 = campana.rotation; bandera0 = bandera.position; plataforma0 = plataforma.position;
        var cam = Camera.main.transform; Log($"[CAMARA] pos={cam.position:F3} rot={cam.eulerAngles:F1} fov={Camera.main.fieldOfView}");
        Log($"[PRUEBA] fixedDelta={Time.fixedDeltaTime} solverIter={Physics.defaultSolverIterations} friccion={fricGen0} friccionDomino={fricDom0} masaDomino={dominos[0].GetComponent<Rigidbody>().mass} masaBola={masaBola0}");

        for (int k = 0; k < repeticiones; k++) yield return Ejecutar(k);

        Terminado = true;
        Log($"[PRUEBA] FIN: {Oks}/{Total} OK");
        Time.captureDeltaTime = 0f;
    }

    IEnumerator Ejecutar(int k)
    {
        var azar = new System.Random(1000 + k);
        float J() => (float)(azar.NextDouble() * 2.0 - 1.0);
        // k==0: sin perturbación; el resto: perturbación aleatoria
        float s = k == 0 ? 0f : 1f;
        float fd = fricDom0 + s * J() * jitterFriccion, fg = fricGen0 + s * J() * jitterFriccion, mb = masaBola0 + s * J() * jitterMasaBola;
        matDomino.dynamicFriction = matDomino.staticFriction = Mathf.Max(0.02f, fd);
        matGeneral.dynamicFriction = matGeneral.staticFriction = Mathf.Max(0.05f, fg);
        rb1.mass = rb2.mass = mb;
        m.Reiniciar();
        yield return new WaitForSeconds(0.4f);
        Vector3 off = new Vector3(s * J() * jitterPosicion, 0f, s * J() * jitterPosicion);
        rb1.position += off; bola1.position += off;

        float t0 = Time.time; zoomN = 0;
        bool b1sale = false, d1 = false, d23 = false, meta = false;
        float tb1 = 0, td1 = 0, td23 = 0, tmeta = 0, campMax = 0, v2max = 0;
        int caidosMax = 0;
        Vector3 b1ini = bola1.position;
        float ultimoDet = -1f;
        float tCampPico = 0, tPrimerGolpe = -1, campFinal = 0; int cruces = 0; float signoPrev = 0;
        var campCurva = new List<float>();

        if (modo == Modo.Completo) m.Arrancar();
        else rb2.linearVelocity = new Vector3(-0.6f, 0f, 0f);

        while (true)
        {
            yield return new WaitForFixedUpdate();
            float t = Time.time - t0;
            int caidos = 0;
            foreach (var d in dominos) if (d.up.y < 0.6f) caidos++;
            caidosMax = Mathf.Max(caidosMax, caidos);
            if (!b1sale && Vector3.Distance(bola1.position, b1ini) > 0.05f) { b1sale = true; tb1 = t; }
            if (!d1 && dominos[0].up.y < 0.6f) { d1 = true; td1 = t; }
            if (!d23 && dominos[22].up.y < 0.6f) { d23 = true; td23 = t; }
            float angC = Quaternion.Angle(campanaRot0, campana.localRotation);
            // ángulo con signo respecto al eje de la barra (Z mundo)
            var delta = campana.rotation * Quaternion.Inverse(campanaRotW0); delta.ToAngleAxis(out float aa, out Vector3 ax);
            float angS = (aa > 180f ? aa - 360f : aa) * (Vector3.Dot(ax, Vector3.forward) >= 0f ? 1f : -1f);
            if (angC > campMax) { campMax = angC; tCampPico = t; }
            if (tPrimerGolpe < 0 && angC > 2f) tPrimerGolpe = t;
            if (tPrimerGolpe >= 0)
            {
                float sg = Mathf.Abs(angS) < 0.5f ? signoPrev : Mathf.Sign(angS);
                if (signoPrev != 0 && sg != signoPrev) cruces++;
                signoPrev = sg;
                if (campCurva.Count < 3000 && (campCurva.Count == 0 || Time.frameCount % 5 == 0)) campCurva.Add(angS);
            }
            campFinal = angS;
            v2max = Mathf.Max(v2max, rb2.linearVelocity.magnitude);
            if (!meta && m.Celebrando) { meta = true; tmeta = t; }
            if (k < 1 && zoomN < 45 && bola2.position.x < -1.72f && bola2.position.y < 0.2f && bola2.position.z > 0.6f && t - ultimoDet >= 0.04f)
            {
                ultimoDet = t; zoomN++;
                var rbc = campana.GetComponent<Rigidbody>(); var hj = campana.GetComponent<HingeJoint>();
                var cc = campana.GetComponent<Collider>(); var bc = bola2.GetComponent<Collider>();
                Vector3 cp = cc.ClosestPoint(bola2.position);
                Log($"[ZOOM] t={t:F2} b2={bola2.position:F3} v={rb2.linearVelocity:F2} | campana pos={campana.position:F3} ang={angS:F1} w={rbc.angularVelocity.magnitude:F2} sleeping={rbc.IsSleeping()} | distCentroBola-Campana={Vector3.Distance(cp, bola2.position):F3} | hingeAnchorW={campana.TransformPoint(hj.anchor):F3} axisW={campana.TransformDirection(hj.axis):F2}");
            }
            if (detalle && t - ultimoDet >= 0.5f)
            {
                ultimoDet = t;
                Log($"[DET] t={t:F2} b1={bola1.position:F2} b2={bola2.position:F2} caidos={caidos} camp={Quaternion.Angle(campanaRot0, campana.localRotation):F0}");
            }
            if (meta && t - tmeta > 3.5f) break;
            if (t > tiempoMaximo) break;
        }

        string curva = ""; for (int i = 0; i < campCurva.Count; i += Mathf.Max(1, campCurva.Count / 14)) curva += campCurva[i].ToString("F0") + " ";
        if (k < 2) Log($"[CAMPANA] #{k} primerGolpe@{tPrimerGolpe:F1}s pico={campMax:F0}°@{tCampPico:F1}s cruces={cruces} final={campFinal:F1}° curva(°)= {curva}");
        float sube = bandera.position.y - bandera0.y, baja = plataforma0.y - plataforma.position.y;
        bool ok = meta && sube > 0.25f && caidosMax == 23 || (modo == Modo.SoloBola2 && meta && sube > 0.25f);
        Total++; if (ok) Oks++;
        Log($"[PRUEBA] #{k} {(ok ? "OK   " : "FALLO")} | b1@{tb1:F1}s d01@{td1:F1}s d23@{td23:F1}s meta@{tmeta:F1}s | caidos={caidosMax}/23 campanaMax={campMax:F0}° v2max={v2max:F2} bandera+{sube:F3} plat-{baja:F3} | fricDom={matDomino.dynamicFriction:F2} fric={matGeneral.dynamicFriction:F2} mBola={mb:F2} off=({off.x * 1000:F1},{off.z * 1000:F1})mm | b1={bola1.position:F2} b2={bola2.position:F2}");
    }

    void OnDestroy() { Time.captureDeltaTime = 0f; }
}
