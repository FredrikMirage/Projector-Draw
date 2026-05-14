using UnityEngine;

public class ButtonEnableDisableGO : MonoBehaviour
{
    [SerializeField] GameObject[] setUsActive;
    [SerializeField] GameObject[] setUsDisabled;

    private void Start()
    {
        for (int i = 0; i < setUsActive.Length; i++)
        {
            setUsActive[i].SetActive(false);
        }
    }

    public void TurnOn()
    {
        for (int i = 0; i < setUsActive.Length; i++)
        {
            setUsActive[i].SetActive(true);
        }
    }

    public void TurnOff()
    {
        for (int i = 0; i < setUsActive.Length; i++)
        {
            setUsDisabled[i].SetActive(false);
        }
    }
}
