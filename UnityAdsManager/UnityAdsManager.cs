using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Advertisements;
//using UnityEngine.Monetization;

public class UnityAdsManager : MonoBehaviour, IUnityAdsListener, IAdsNetworkHelper
{
#if DEBUG_ADS
    bool testMode = true;
#else
    bool testMode = false;
#endif
    [SerializeField] bool showBannerOnStart;
    static string currentRewardId;
    static RewardDelegate onRewardWatched;
    public static float timeoutRequestAds = 7f;

    bool? bannerLoadSuccess; //last banner success load result

    AdsManager.InterstitialDelegate onInterstitialClosed;

    public static UnityAdsManager instance;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        //Monetization.Initialize(Const.GAMEID, testMode);
        Advertisement.Initialize(CustomMediation.unityGameId, testMode);
        if (showBannerOnStart)
            StartCoroutine(ShowBannerWhenReady(CustomMediation.GetUnityPlacementId(AdPlacement.Banner)));
        //interstitialSplashContent = Monetization.GetPlacementContent(interstitialSplash) as ShowAdPlacementContent;

        Advertisement.AddListener(this);
    }

    public void RequestInterstitialNoShow(string placementId, AdsManager.InterstitialDelegate onAdLoaded = null, bool showLoading = true)
    {
        StartCoroutine(CoRequestInterstitial(placementId, onAdLoaded, showLoading));
    }

    IEnumerator CoRequestInterstitial(string placementId, AdsManager.InterstitialDelegate onAdLoaded = null, bool showLoading = true)
    {
        float _timeoutRequestAds = timeoutRequestAds;
        PlacementState adState = PlacementState.Waiting;
        float retryInterval = 0.4f;
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(retryInterval);
        int tryTimes = 0;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("unity ad not reachable " + Application.internetReachability);
            _timeoutRequestAds = 3f;
        }
        while (adState != PlacementState.Ready && tryTimes < _timeoutRequestAds / retryInterval)
        {
            adState = Advertisement.GetPlacementState(placementId);
            if (adState != PlacementState.Ready)
            {
                yield return delay;
                tryTimes++;
            }
        }
        Debug.Log("Unity request ad state " + adState);
        onAdLoaded?.Invoke(adState == PlacementState.Ready);
        //if (showLoading)
        //    Manager.LoadingAnimation(false);
    }

    public static void ShowBanner(string placementId, AdsManager.InterstitialDelegate onAdLoaded = null)
    {
        instance.StartCoroutine(instance.ShowBannerWhenReady(placementId, onAdLoaded));
    }

    IEnumerator ShowBannerWhenReady(string placementId, AdsManager.InterstitialDelegate onAdLoaded = null)
    {
        Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);

        BannerLoadOptions options = new BannerLoadOptions { loadCallback = OnLoadBannerSuccess, errorCallback = OnLoadBannerFail };
        Advertisement.Banner.Load(placementId, options);

        float timeoutTime = 5f, retryInterval = 0.2f;
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(retryInterval);
        int tryTimes = 0;
        while (!bannerLoadSuccess.HasValue && tryTimes < timeoutTime / retryInterval)
        {
            yield return delay;
            tryTimes++;
        }

        if (!bannerLoadSuccess.HasValue)
        {
            LogEvent("LoadBannerTimeout", "", "");
            bannerLoadSuccess = false;
        }
        onAdLoaded?.Invoke(bannerLoadSuccess.Value);

        bool adReady = Advertisement.IsReady(placementId);
        PlacementState adState = Advertisement.GetPlacementState(placementId);
        if (bannerLoadSuccess.Value)
        {
            Debug.Log($"Unity banner showing. State: {Advertisement.GetPlacementState(placementId)}, ready {adReady}");
            Advertisement.Banner.Show(placementId);
        }
        else
        {
            Debug.Log($"Unity banner show failed. State: {Advertisement.GetPlacementState(placementId)}, ready {adReady}");
        }

        /*if (adReady) //show banner regardless of load success or not since Unity Ads is the only ads
            Advertisement.Banner.Show(placementId);*/
        bannerLoadSuccess = null;
    }

    private void OnLoadBannerSuccess()
    {
        bannerLoadSuccess = true;
    }

    private void OnLoadBannerFail(string message)
    {
        bannerLoadSuccess = false;
        Debug.Log($"Unity banner load failed. {message}");
        LogEvent("LoadBannerFailed", "message", message);
    }

    public void HideBanner()
    {
        Advertisement.Banner.Hide();
    }

    public static void ShowInterstitial(string placementId, AdsManager.InterstitialDelegate onAdClosed)
    {
        Advertisement.Show(placementId);
    }

    public static void Reward(RewardDelegate onFinish, string placementId)
    {
        currentRewardId = placementId;
        onRewardWatched = onFinish;

        if (Advertisement.IsReady(currentRewardId))
            Advertisement.Show(currentRewardId);
        else
        {
            if (AdsManager.HasNoInternet())
            {
                onRewardWatched?.Invoke(new RewardResult(RewardResult.Type.LoadFailed, "No internet connection."));
            }
            else
            {
                //Manager.LoadingAnimation(true); //common AdsManager will handle turning off loading
                instance.RequestInterstitialNoShow(currentRewardId, (loadSuccess) =>
                {
                    if (loadSuccess)
                    {
                        Debug.Log("Load reward ad success");
                        Advertisement.Show(currentRewardId);
                    }
                    else
                    {
                        string error = "Unity Reward failed " + Advertisement.GetPlacementState(currentRewardId).ToString();
                        Debug.Log(error);
                        onRewardWatched?.Invoke(new RewardResult(RewardResult.Type.LoadFailed, error));
                        //AdsManager.ShowError(Advertisement.GetPlacementState(currentRewardId).ToString());
                    }
                }, showLoading: true);
            }
        }
    }

    // Implement IUnityAdsListener interface methods:
    public void OnUnityAdsDidFinish(string placementId, UnityEngine.Advertisements.ShowResult showResult)
    {
        if (!string.IsNullOrEmpty(currentRewardId) && string.Equals(currentRewardId, placementId))
        {
            // Define conditional logic for each ad completion status:
            if (showResult == UnityEngine.Advertisements.ShowResult.Finished)
            {
                onRewardWatched?.Invoke(new RewardResult(RewardResult.Type.Finished));
                // Reward the user for watching the ad to completion.
            }
            else if (showResult == UnityEngine.Advertisements.ShowResult.Skipped)
            {
                onRewardWatched?.Invoke(new RewardResult(RewardResult.Type.Canceled));
                Debug.Log("skipped ad");
                // Do not reward the user for skipping the ad.
            }
            else if (showResult == UnityEngine.Advertisements.ShowResult.Failed)
            {
                onRewardWatched?.Invoke(new RewardResult(RewardResult.Type.LoadFailed));
                AdsManager.ShowError(Advertisement.GetPlacementState(currentRewardId).ToString(), placementId);
                Debug.LogWarning("The ad did not finish due to an error.");
            }
            onRewardWatched = null;
        }
        else if (onInterstitialClosed != null) //closing a interstitial ads
        {
            onInterstitialClosed.Invoke(showResult == UnityEngine.Advertisements.ShowResult.Finished);
            onInterstitialClosed = null;
        }
    }

    public void OnUnityAdsReady(string placementId)
    {
        // If the ready Placement is rewarded, show the ad:
        if (placementId == currentRewardId)
        {
            Debug.Log("OnUnityAdsReady");
            //Advertisement.Show(currentRewardId);
        }
    }

    public void OnUnityAdsDidError(string message)
    {
        Debug.Log("unity ad error handler: " + message);
        // Log the error.
    }

    public void OnUnityAdsDidStart(string placementId)
    {
        // Optional actions to take when the end-users triggers an ad.
    }

    public void ShowBanner(AdPlacement.Type placementType, AdsManager.InterstitialDelegate onAdLoaded = null)
    {
        ShowBanner(CustomMediation.GetUnityPlacementId(placementType), onAdLoaded);
    }

    public void ShowInterstitial(AdPlacement.Type placementType, AdsManager.InterstitialDelegate onAdClosed)
    {
        onInterstitialClosed = onAdClosed;
        ShowInterstitial(CustomMediation.GetUnityPlacementId(placementType), onAdClosed);
    }

    public void RequestInterstitialNoShow(AdPlacement.Type placementType, AdsManager.InterstitialDelegate onAdLoaded = null, bool showLoading = true)
    {
        RequestInterstitialNoShow(CustomMediation.GetUnityPlacementId(placementType), onAdLoaded, showLoading);
    }

    public void Reward(AdPlacement.Type placementType, RewardDelegate onFinish)
    {
        Reward(onFinish, CustomMediation.GetUnityPlacementId(placementType));
    }

    static void LogEvent(string message, string param, string value)
    {
#if FIREBASE
        FirebaseManager.LogEvent($"UnityAds_{message}", param, value);
#endif
    }

    public void RequestInterstitialRewardedNoShow(AdPlacement.Type placementType, RewardDelegate onFinish = null)
    {
        onFinish?.Invoke(new RewardResult(RewardResult.Type.LoadFailed, "Not supported by Unity Ads"));
    }

    public void ShowInterstitialRewarded(AdPlacement.Type placementType, RewardDelegate onAdClosed)
    {
        onAdClosed?.Invoke(new RewardResult(RewardResult.Type.LoadFailed, "Not supported by Unity Ads"));
    }
}
