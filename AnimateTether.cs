using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimateTether : MonoBehaviour
{
    private LineRenderer LineRenderer;
    private float AnimationSpeed = .07f;
    private float Offset = .13f;
    private float _frameTime;

    // Start is called before the first frame update
    void Start()
    {
        LineRenderer = GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        _frameTime += Time.deltaTime;
        if (_frameTime > AnimationSpeed)
        {
            _frameTime = 0f;
            LineRenderer.material.mainTextureOffset = new Vector2(0f, LineRenderer.material.mainTextureOffset.y + Offset);
            if (LineRenderer.material.mainTextureOffset.y > 1f) LineRenderer.material.mainTextureOffset = new Vector2(0f, 0f);
        }
    }
}
