using UnityEngine;
using UnityEngine.SceneManagement;

public class CreditsSceneUI : MonoBehaviour
{
    void Update()
    {
        // 마우스 클릭 또는 엔터키로 타이틀 복귀
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
        {
            ReturnToTitle();
        }
    }

    void ReturnToTitle()
    {
        Debug.Log("Returning to TitleScene");
        SceneManager.LoadScene("TitleScene");
    }
}