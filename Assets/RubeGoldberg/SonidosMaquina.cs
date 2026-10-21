using UnityEngine;

/// <summary>
/// Sonidos de la máquina. MaquinaRubeGoldberg lo añade y lo configura solo.
/// Si arrastras tus propios AudioClip a los campos, se usan esos; si se dejan vacíos,
/// los sonidos se generan por código (no hace falta ningún archivo de audio).
/// </summary>
public class SonidosMaquina : MonoBehaviour
{
    [Header("Opcional: tus propios sonidos (vacío = se generan por código)")]
    public AudioClip choqueDomino;
    public AudioClip golpeBola;
    public AudioClip rodarBola;
    public AudioClip tickEngranaje;
    public AudioClip campana;
    public AudioClip fanfarria;

    [Range(0f, 1f)] public float volumenGeneral = 0.9f;

    AudioSource fuente2D;
    bool configurado;

    public void Configurar(Transform raiz)
    {
        if (configurado) return;
        configurado = true;

        if (choqueDomino == null) choqueDomino = Sintetizador.ChoqueMadera("ChoqueDomino", 1150f, 0.05f);
        if (golpeBola == null) golpeBola = Sintetizador.ChoqueMadera("GolpeBola", 520f, 0.08f);
        if (rodarBola == null) rodarBola = Sintetizador.Rodar("RodarBola", 2f);
        if (tickEngranaje == null) tickEngranaje = Sintetizador.Tick("TickEngranaje");
        if (campana == null) campana = Sintetizador.Campana("Campana", 1046.5f, 3f);
        if (fanfarria == null) fanfarria = Sintetizador.Fanfarria("Fanfarria");

        fuente2D = gameObject.AddComponent<AudioSource>();
        fuente2D.playOnAwake = false;
        fuente2D.spatialBlend = 0f;

        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Rigidbody>() == null) continue;
            if (t.name.StartsWith("Domino_"))
            {
                Choque(t, choqueDomino, 0.08f, 0.85f, 1.2f, 0.8f);
            }
            else if (t.name.StartsWith("Bola_"))
            {
                Choque(t, golpeBola, 0.12f, 0.9f, 1.1f, 0.7f);
                var r = t.gameObject.AddComponent<RodadoSonoro>();
                r.clip = rodarBola;
                r.volumenMax = 0.45f * volumenGeneral;
            }
            else if (t.name == "Campana")
            {
                Choque(t, campana, 0.05f, 0.99f, 1.01f, 1f);
            }
        }
    }

    void Choque(Transform t, AudioClip clip, float umbral, float pitchMin, float pitchMax, float volumen)
    {
        var c = t.gameObject.AddComponent<ChoqueSonoro>();
        c.clip = clip;
        c.umbral = umbral;
        c.pitchMin = pitchMin;
        c.pitchMax = pitchMax;
        c.volumenMax = volumen * volumenGeneral;
    }

    public void Tick(Vector3 posicion)
    {
        AudioSource.PlayClipAtPoint(tickEngranaje, posicion, 0.35f * volumenGeneral);
    }

    public void Fanfarria()
    {
        if (fuente2D != null) fuente2D.PlayOneShot(fanfarria, volumenGeneral);
    }
}

/// <summary>Genera los sonidos de la máquina por síntesis (44.1 kHz, mono).</summary>
static class Sintetizador
{
    const int SR = 44100;
    static readonly System.Random azar = new System.Random(12345);
    static float Ruido() => (float)(azar.NextDouble() * 2.0 - 1.0);
    static float Sen(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

    static AudioClip Crear(string nombre, float[] datos, float pico = 0.9f)
    {
        float max = 1e-6f;
        foreach (var x in datos) max = Mathf.Max(max, Mathf.Abs(x));
        for (int i = 0; i < datos.Length; i++) datos[i] *= pico / max;
        var clip = AudioClip.Create(nombre, datos.Length, 1, SR, false);
        clip.SetData(datos, 0);
        return clip;
    }

    /// Golpe seco de madera: un tono corto con armónico inarmónico y un chasquido de ruido.
    public static AudioClip ChoqueMadera(string nombre, float f, float caida)
    {
        int n = (int)(SR * caida * 5f);
        var d = new float[n];
        float filtrado = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            filtrado += (Ruido() - filtrado) * 0.35f;
            float tono = 0.6f * Sen(f, t) * Mathf.Exp(-t / caida)
                       + 0.3f * Sen(f * 2.76f, t) * Mathf.Exp(-t / (caida * 0.3f));
            d[i] = tono + 0.9f * filtrado * Mathf.Exp(-t / 0.004f);
        }
        return Crear(nombre, d, 0.8f);
    }

    /// Clic metálico corto de un diente de engranaje.
    public static AudioClip Tick(string nombre)
    {
        int n = (int)(SR * 0.03f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            d[i] = Mathf.Exp(-t / 0.0025f) * (0.6f * Ruido() + 0.6f * Sen(3200f, t));
        }
        return Crear(nombre, d, 0.7f);
    }

    /// Ruido grave y continuo de una bola rodando (en bucle sin cortes).
    public static AudioClip Rodar(string nombre, float duracion)
    {
        int n = (int)(SR * duracion);
        int cruce = SR / 10;   // la cola extra se funde con el principio para que el bucle no se note
        var d = new float[n + cruce];
        float marron = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / SR;
            marron = marron * 0.995f + Ruido() * 0.05f;
            d[i] = marron * (0.8f + 0.2f * Sen(7f, t)) + 0.15f * Sen(85f, t);
        }
        var bucle = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (i < cruce)
            {
                float a = (float)i / cruce;
                bucle[i] = d[i] * a + d[n + i] * (1f - a);
            }
            else bucle[i] = d[i];
        }
        return Crear(nombre, bucle, 0.6f);
    }

    /// Campana: suma de parciales inarmónicos con caídas distintas.
    public static AudioClip Campana(string nombre, float f0, float duracion)
    {
        float[] razon = { 0.5f, 1f, 1.183f, 1.506f, 2f, 2.514f, 2.662f, 3.011f };
        float[] amp = { 0.35f, 1f, 0.6f, 0.45f, 0.35f, 0.25f, 0.2f, 0.12f };
        float[] caida = { 2.5f, 2f, 1.6f, 1.2f, 0.9f, 0.7f, 0.6f, 0.5f };
        int n = (int)(SR * duracion);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = 0f;
            for (int k = 0; k < razon.Length; k++)
                s += amp[k] * Sen(f0 * razon[k], t) * Mathf.Exp(-t / (caida[k] * duracion / 3f));
            d[i] = s * Mathf.Min(1f, t / 0.002f);
        }
        return Crear(nombre, d, 0.85f);
    }

    /// Fanfarria corta: arpegio Do-Mi-Sol-Do y acorde final.
    public static AudioClip Fanfarria(string nombre)
    {
        float[] notas = { 523.25f, 659.25f, 783.99f, 1046.5f };
        int n = (int)(SR * 2.2f);
        var d = new float[n];
        for (int k = 0; k < notas.Length; k++)
        {
            float inicio = 0.14f * k;
            float fin = k < 3 ? inicio + 0.2f : 2.2f;
            AnadirNota(d, notas[k], inicio, fin);
        }
        for (int k = 0; k < 3; k++) AnadirNota(d, notas[k], 0.42f, 2.2f, 0.6f);   // acorde
        return Crear(nombre, d, 0.8f);
    }

    static void AnadirNota(float[] d, float f, float inicio, float fin, float volumen = 1f)
    {
        int a = (int)(inicio * SR), b = Mathf.Min(d.Length, (int)(fin * SR));
        for (int i = a; i < b; i++)
        {
            float t = (float)(i - a) / SR;
            float dur = (float)(b - a) / SR;
            float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t / (dur * 0.6f)) * Mathf.Min(1f, (dur - t) / 0.05f);
            float s = 0f;
            for (int h = 1; h <= 6; h++) s += Sen(f * h, t) / (h * h * 0.7f + 0.3f);
            d[i] += volumen * env * s;
        }
    }
}
