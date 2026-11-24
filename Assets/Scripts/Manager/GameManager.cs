using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class GameManager : MonoBehaviour
{

    public Button btn_Drift, btn_Right, btn_Left, btn_boast;
    public FinishLine finishLine;
    // Start is called before the first frame update
    void Start()
    {
        finishLine = GameObject.Find("finishLineBoard").GetComponent<FinishLine>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (finishLine.bolGameFinished)
        {
            btn_Drift.gameObject.SetActive(false);
            btn_Right.gameObject.SetActive(false);
            btn_Left.gameObject.SetActive(false);
            btn_boast.gameObject.SetActive(false);
        }
    }
}
