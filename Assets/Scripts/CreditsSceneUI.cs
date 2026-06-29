using UnityEngine;

public class CreditsSceneUI : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
        {
            GlobalManager.Instance.LoadScene("TitleScene");
        }
    }
}
