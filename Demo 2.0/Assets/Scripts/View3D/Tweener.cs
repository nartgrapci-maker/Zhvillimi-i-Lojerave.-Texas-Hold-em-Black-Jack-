using UnityEngine;

/// <summary>Smoothly moves / rotates / scales an object. Used for dealing cards, flipping them and moving chips.</summary>
public class Tweener : MonoBehaviour
{
    Vector3 _p0, _p1, _s0, _s1;
    Quaternion _r0, _r1;
    float _t, _dur = 0.4f, _delay, _lift;
    bool _moving;
    float _st, _sdur = 0.2f, _sdelay;
    bool _scaling;

    public void Go(Vector3 pos, Quaternion rot, float duration, float startDelay = 0f, float arcLift = 0f)
    {
        _p0 = transform.position;
        _r0 = transform.rotation;
        _p1 = pos;
        _r1 = rot;
        _dur = Mathf.Max(0.01f, duration);
        _delay = startDelay;
        _lift = arcLift;
        _t = 0f;
        _moving = true;
    }

    public void Scale(Vector3 from, Vector3 to, float duration, float startDelay = 0f)
    {
        _s0 = from;
        _s1 = to;
        _sdur = Mathf.Max(0.01f, duration);
        _sdelay = startDelay;
        _st = 0f;
        _scaling = true;
        transform.localScale = from;
    }

    void Update()
    {
        if (_moving)
        {
            if (_delay > 0f) _delay -= Time.deltaTime;
            else
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / _dur);
                float e = k * k * (3f - 2f * k);
                transform.position = Vector3.Lerp(_p0, _p1, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * _lift);
                transform.rotation = Quaternion.Slerp(_r0, _r1, e);
                if (k >= 1f) _moving = false;
            }
        }
        if (_scaling)
        {
            if (_sdelay > 0f) _sdelay -= Time.deltaTime;
            else
            {
                _st += Time.deltaTime;
                float k = Mathf.Clamp01(_st / _sdur);
                float e = 1f - (1f - k) * (1f - k);
                transform.localScale = Vector3.Lerp(_s0, _s1, e);
                if (k >= 1f) _scaling = false;
            }
        }
    }
}
