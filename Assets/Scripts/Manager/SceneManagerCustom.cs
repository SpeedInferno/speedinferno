using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
public class SceneManagerCustom : MonoBehaviour
{

   private Slider sliderLoading;
   private TextMeshProUGUI progressPercentage;

   void Start()
   {
      if (SceneManager.GetActiveScene().name == "Loading1")
      {  
         sliderLoading = GameObject.Find("LoadingSlider").GetComponent<Slider>();
         progressPercentage = GameObject.Find("txt_Progress").GetComponent<TextMeshProUGUI>();
         fun_LoadMainGame();
      }
   }

   public void fun_LoadTitleScene()
   {
      SceneManager.LoadScene(sceneName:"Title");
   }
   public void fun_LoadMainGame()
   {
      AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName:"MainGame");
      StartCoroutine(fun_loadingStatus(operation));
   }

   IEnumerator fun_loadingStatus(AsyncOperation loadingOperation)
   {
      while(!loadingOperation.isDone)
      {
         float percentage = Mathf.Clamp(loadingOperation.progress/0.9f,0,1)*100;
         progressPercentage.text = ((int)percentage) + "%";
         Debug.Log("Progress" + percentage);
         sliderLoading.value = percentage;
         yield return null;
      }
   
   }

   public void fun_loadMapSelection()
   {
      SceneManager.LoadScene(sceneName:"MapSelection");
   }
   public void fun_LoadLoadingScene1()
   {
      SceneManager.LoadScene(sceneName:"Loading1");
   }
   public void fun_ExitGame()
   {
      Application.Quit();
   }


   public void fun_loadActiveScene()
   {
      SceneManager.LoadScene(SceneManager.GetActiveScene().name);
   }

}
