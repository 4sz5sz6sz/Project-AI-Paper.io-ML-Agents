using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections;

public class ButtonController : MonoBehaviour{
    public void OnButtonClicked()
    {
        SceneManager.LoadScene("GetDifficulty");
    }
}
