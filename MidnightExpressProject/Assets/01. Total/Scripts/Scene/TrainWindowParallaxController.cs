using System;
using UnityEngine;

public sealed class TrainWindowParallaxController : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");
    private static readonly int SeamBlendWidthId = Shader.PropertyToID("_SeamBlendWidth");

    [Serializable]
    private sealed class ParallaxLayer
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField, Min(0f)] private float _horizontalSpeed;
        [SerializeField, Range(0.001f, 0.1f)] private float _seamBlendWidth = 0.02f;

        private Material _runtimeMaterial;

        public void Initialize(Shader shader)
        {
            if (_renderer == null || _renderer.sprite == null || shader == null)
            {
                return;
            }

            _runtimeMaterial = new Material(shader)
            {
                name = $"{_renderer.name} Parallax (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            _runtimeMaterial.SetTexture(MainTexId, _renderer.sprite.texture);
            _runtimeMaterial.SetFloat(SeamBlendWidthId, _seamBlendWidth);
            _renderer.material = _runtimeMaterial;
        }

        public void UpdateOffset(float travel)
        {
            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.SetFloat(ScrollOffsetId, Mathf.Repeat(travel * _horizontalSpeed, 1f));
            }
        }

        public void Dispose()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
        }
    }

    [Header("Parallax")]
    [SerializeField] private Shader _parallaxShader;
    [SerializeField] private ParallaxLayer _frontBuildings = new();
    [SerializeField] private ParallaxLayer _backBuildings = new();
    [SerializeField] private ParallaxLayer _cloud = new();
    [SerializeField] private ParallaxLayer _sky = new();
    [SerializeField, Min(0f)] private float _speedMultiplier = 1f;

    [Header("Train Rumble")]
    [SerializeField] private Transform[] _rumbleTargets = Array.Empty<Transform>();
    [SerializeField, Min(0f)] private float _verticalAmplitude = 0.0012f;
    [SerializeField, Min(0f)] private float _horizontalAmplitude = 0.0004f;
    [SerializeField, Min(0.01f)] private float _rumbleFrequency = 1.25f;
    [SerializeField, Min(0f)] private float _rotationAmplitude = 0.0025f;
    [SerializeField, Min(0f)] private float _strongBumpAmount = 0.014f;
    [SerializeField, Min(0.1f)] private float _strongBumpInterval = 3f;
    [SerializeField, Min(0.05f)] private float _strongBumpDuration = 0.36f;

    private Vector3[] _rumbleBaseLocalPositions = Array.Empty<Vector3>();
    private Quaternion[] _rumbleBaseLocalRotations = Array.Empty<Quaternion>();
    private float _travel;

    private void Awake()
    {
        _rumbleBaseLocalPositions = new Vector3[_rumbleTargets.Length];
        _rumbleBaseLocalRotations = new Quaternion[_rumbleTargets.Length];
        for (int index = 0; index < _rumbleTargets.Length; index++)
        {
            Transform target = _rumbleTargets[index];
            if (target == null)
            {
                continue;
            }

            _rumbleBaseLocalPositions[index] = target.localPosition;
            _rumbleBaseLocalRotations[index] = target.localRotation;
        }

        _frontBuildings.Initialize(_parallaxShader);
        _backBuildings.Initialize(_parallaxShader);
        _cloud.Initialize(_parallaxShader);
        _sky.Initialize(_parallaxShader);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        _travel += deltaTime * _speedMultiplier;

        _frontBuildings.UpdateOffset(_travel);
        _backBuildings.UpdateOffset(_travel);
        _cloud.UpdateOffset(_travel);
        _sky.UpdateOffset(_travel);
    }

    private void LateUpdate()
    {
        float time = Time.time;
        float phase = time * _rumbleFrequency * Mathf.PI * 2f;
        float secondary = Mathf.Sin(phase * 0.47f + 1.3f);
        float vertical = (Mathf.Sin(phase) * 0.72f + secondary * 0.28f) * _verticalAmplitude;
        float horizontal = Mathf.Sin(phase * 0.61f + 0.8f) * _horizontalAmplitude;

        float timeSinceBump = Mathf.Repeat(time, _strongBumpInterval);
        if (timeSinceBump < _strongBumpDuration)
        {
            float bumpPhase = timeSinceBump / _strongBumpDuration;
            float bumpEnvelope = Mathf.Sin(bumpPhase * Mathf.PI);
            vertical += Mathf.Sin(bumpPhase * Mathf.PI * 2f) * bumpEnvelope * _strongBumpAmount;
        }

        Vector3 rumbleOffset = new(horizontal, vertical, 0f);
        Quaternion rumbleRotation = Quaternion.Euler(0f, 0f, secondary * _rotationAmplitude);
        for (int index = 0; index < _rumbleTargets.Length; index++)
        {
            Transform target = _rumbleTargets[index];
            if (target == null)
            {
                continue;
            }

            target.localPosition = _rumbleBaseLocalPositions[index] + rumbleOffset;
            target.localRotation = _rumbleBaseLocalRotations[index] * rumbleRotation;
        }
    }

    private void OnDisable()
    {
        for (int index = 0; index < _rumbleTargets.Length; index++)
        {
            Transform target = _rumbleTargets[index];
            if (target == null)
            {
                continue;
            }

            target.localPosition = _rumbleBaseLocalPositions[index];
            target.localRotation = _rumbleBaseLocalRotations[index];
        }
    }

    private void OnDestroy()
    {
        _frontBuildings.Dispose();
        _backBuildings.Dispose();
        _cloud.Dispose();
        _sky.Dispose();
    }
}
