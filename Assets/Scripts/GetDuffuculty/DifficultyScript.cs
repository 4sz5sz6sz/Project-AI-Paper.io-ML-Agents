using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections;

public class DifficultyScript : MonoBehaviour
{
    public void EasyButtonClicked()
    {
        SceneManager.LoadScene("TW");
    }
    public void MediumButtonClicked()
    {
        SceneManager.LoadScene("JM");
    }
    public void HardButtonClicked()
    {
        SceneManager.LoadScene("TG");
    }
}
