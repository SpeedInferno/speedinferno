using System.Collections;
using System.Threading;
using UnityEngine;
public class FpsCode : MonoBehaviour
{
[Header("Frame Settings")]
int MaxRate = 9999;
public float TargetFrameRate = 60.0f;

void Awake()
{
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = MaxRate;


}

}