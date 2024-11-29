using UnityEngine;

namespace SaintsField.Samples.Scripts.HandleExamples
{
    public class DrawLabelExample : MonoBehaviour
    {
        [DrawLabel("Test"), GetComponent] public GameObject thisObj;
        [DrawLabel(EColor.Yellow ,"Child")] public GameObject child;
    }
}
