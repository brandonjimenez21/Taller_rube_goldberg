using UnityEngine;

/// <summary>
/// Zona de meta (dentro del hueco). Lo añade automáticamente MaquinaRubeGoldberg
/// al objeto "Meta_Trigger"; avisa a la máquina cuando entra una bola.
/// </summary>
public class MetaTrigger : MonoBehaviour
{
    public MaquinaRubeGoldberg maquina;

    void OnTriggerEnter(Collider other)
    {
        if (maquina != null && other.attachedRigidbody != null && other.name.StartsWith("Bola"))
            maquina.Celebrar();
    }
}
