using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.Events;

public class UnityAdListener : MonoBehaviour, IUnityAdsListener
{
    public string MyPlacementId;
    public UnityEvent onAdsReady;
    public UnityEvent<bool> onAdsStart;
    public UnityEvent onAdsFinish;

    public void OnUnityAdsDidError(string message)
    {
        Debug.Log(message);
        onAdsStart.Invoke(false);
    }

    public void OnUnityAdsDidFinish(string placementId, ShowResult showResult)
    {
        if (!string.IsNullOrEmpty(MyPlacementId))
        {
            if (string.Equals(MyPlacementId, placementId))
            {
                onAdsFinish.Invoke();
            }
        }
        else
        {
            onAdsFinish.Invoke();
        }
    }

    public void OnUnityAdsDidStart(string placementId)
    {
        onAdsStart.Invoke(true);
    }

    public void OnUnityAdsReady(string placementId)
    {
        onAdsReady.Invoke();
    }
}
