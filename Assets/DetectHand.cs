﻿using System.Collections.Generic;
using OpenCVForUnity;
using OpenCVForUnityExample;
using UnityEngine;
using static OpenCVForUnity.Imgproc;
using static OpenCVForUnity.Core;

[RequireComponent(typeof(WebCamTextureToMatHelper), typeof(FpsMonitor))]
public class DetectHand : MonoBehaviour
{
    [SerializeField] ParticleSystem _firePrefab;
    
    Texture2D _texture;
    WebCamTexture _webcamtex;
    WebCamTextureToMatHelper _webCamTextureToMatHelper;
    FpsMonitor _fpsMonitor;
    ParticleSystem _fire;
    
    static readonly Scalar SKIN_LOWER = new Scalar(0, 70, 90);
    static readonly Scalar SKIN_UPPER = new Scalar(35, 255, 255);
    static readonly float FIRE_SCALE = 100f;
    static readonly float FIRE_Z_POS = -10f;
    
    void Start()
    {
        Input.backButtonLeavesApp = true;
        
        _fpsMonitor = GetComponent<FpsMonitor>();
        _webCamTextureToMatHelper = GetComponent<WebCamTextureToMatHelper>();
        _webCamTextureToMatHelper.Initialize();
//        GetComponent<Renderer>().material.mainTexture = _webCamTextureToMatHelper.GetWebCamTexture();
        
        _fire = Instantiate(_firePrefab, new Vector3(0f, 0f, FIRE_Z_POS), Quaternion.identity);
        _fire.transform.localScale = new Vector3(FIRE_SCALE, FIRE_SCALE, FIRE_SCALE);
    }
    
    void Update()
    {
        if (!_webCamTextureToMatHelper.IsPlaying() || !_webCamTextureToMatHelper.DidUpdateThisFrame()) return;
        
        var rgbaMat = _webCamTextureToMatHelper.GetMat();
        SetFire(rgbaMat);
//        _texture = _webCamTextureToMatHelper.GetWebCamTexture();
        Utils.fastMatToTexture2D(rgbaMat, _texture);
    }

    void SetFire(Mat rgbaMat)
    {
        //RGBAをHSVに変換
        var rgbMat = new Mat();
        cvtColor(rgbaMat, rgbMat, COLOR_RGBA2RGB);
        cvtColor(rgbMat, rgbMat, COLOR_RGB2HSV);
        
        //肌色領域を抽出
        var handMask = new Mat();
        inRange(rgbMat, SKIN_LOWER, SKIN_UPPER, handMask);
        morphologyEx(handMask, handMask, MORPH_OPEN, new Mat(), new Point(-1, -1), 3);
        morphologyEx(handMask, handMask, MORPH_CLOSE, new Mat(), new Point(-1, -1), 3);

        //ラベリング
        var centroids = new Mat();
        var stats = new Mat();
        var nLabels = connectedComponentsWithStats(handMask, new Mat(), stats, centroids);

        //最大の領域の重心を取得
        var maxAreaIndex = 0;
        var maxArea = 0.0;
        for (int i = 1; i < nLabels; i++) {        //0番目のラベルは背景のため飛ばす
            var area = stats.get(i, CC_STAT_AREA); //https: //teratail.com/questions/112820
            if (area[0] > maxArea) {
                maxArea = area[0];
                maxAreaIndex = i;
            }
        }
        //https://stackoverflow.com/questions/34470927/connectedcomponentswithstats-return-types-and-values-in-java
        var ctrd = new Point(centroids.get(maxAreaIndex, 0)[0], centroids.get(maxAreaIndex, 1)[0]);
        var ctrdWorldPos = new Point((float)ctrd.x - rgbaMat.width() / 2f, rgbaMat.height() / 2f - (float)ctrd.y);
        var firePos = new Vector3((float)ctrdWorldPos.x, (float)ctrdWorldPos.y, FIRE_Z_POS);
        
        //炎を手の位置に移動
        _fire.transform.position = firePos;
    }
    
    public void OnWebCamTextureToMatHelperInitialized ()
    {
        Debug.Log ("OnWebCamTextureToMatHelperInitialized");

        var webCamTextureMat = _webCamTextureToMatHelper.GetMat();
        _texture = new Texture2D (webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGBA32, false);
        GetComponent<Renderer>().material.mainTexture = _texture;
        
        transform.localScale = new Vector3 (webCamTextureMat.cols(), webCamTextureMat.rows(), 1);
        Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

        if (_fpsMonitor != null){
            _fpsMonitor.Add ("width", webCamTextureMat.width().ToString());
            _fpsMonitor.Add ("height", webCamTextureMat.height().ToString());
            _fpsMonitor.Add ("orientation", Screen.orientation.ToString());
        }
        
        float width = webCamTextureMat.width();
        float height = webCamTextureMat.height();
                                
        float widthScale = Screen.width / width;
        float heightScale = Screen.height / height;
        if (widthScale < heightScale) {
            Camera.main.orthographicSize = (width * Screen.height / Screen.width) / 2;
        } else {
            Camera.main.orthographicSize = height / 2;
        }
    }

    public void OnWebCamTextureToMatHelperDisposed ()
    {
        Debug.Log ("OnWebCamTextureToMatHelperDisposed");
        if (_texture != null) {
            Destroy(_texture);
            _texture = null;
        }
    }
    
    void OnDestroy()
    {
        _webCamTextureToMatHelper.Dispose ();
    }
}
