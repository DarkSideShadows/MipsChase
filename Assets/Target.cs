using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Target : MonoBehaviour
{
    public Player m_player;
    public enum eState : int
    {
        kIdle,
        kHopStart,
        kHop,
        kCaught,
        kNumStates
    }

    private Color[] stateColors = new Color[(int)eState.kNumStates]
   {
        new Color(255, 0,   0),
        new Color(0,   255, 0),
        new Color(0,   0,   255),
        new Color(255, 255, 255)
   };

    // External tunables.
    public float m_fHopTime = 0.2f;
    public float m_fHopSpeed = 6.5f;
    public float m_fScaredDistance = 3.0f;
    public int m_nMaxMoveAttempts = 50;

    // Internal variables.
    public eState m_nState;
    public float m_fHopStart;
    public Vector3 m_vHopStartPos;
    public Vector3 m_vHopEndPos;

    void Start()
    {
        // Setup the initial state and get the player GO.
        m_nState = eState.kIdle;
        m_player = GameObject.FindObjectOfType(typeof(Player)) as Player;
    }

    void FixedUpdate()
    {
        GetComponent<Renderer>().material.color = stateColors[(int)m_nState];
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        // Check if this is the player (in this situation it should be!)
        if (collision.gameObject == GameObject.Find("Player"))
        {
            // If the player is diving, it's a catch!
            if (m_player.IsDiving())
            {
                m_nState = eState.kCaught;
                transform.parent = m_player.transform;
                transform.localPosition = new Vector3(0.0f, -0.5f, 0.0f);
            }
        }
    }

    /*
    The target is the rabbit. It should behave in the following fashion:

        - Stay in one place until the player gets close.
        - Hop when the player gets too close without going off the screen, and avoid the player. Off screen should not happen, but within range of the player is okay if it does not find a way out. Hop should be a visible movement, though it can take place quickly.
        - Attaches itself to the player when it is caught.
    */
    void Update()
    {
        switch (m_nState)
        {
            case eState.kIdle:
                CheckPlayerDistance();
                break;
            case eState.kHopStart:
                StartHop();
                break;
            case eState.kHop:
                PerformHop();
                break;
            case eState.kCaught:
                // handled in trigger
                break;
        }
    }

    void CheckPlayerDistance()
    {
        if (Vector3.Distance(transform.position, m_player.transform.position) < m_fScaredDistance)
            m_nState = eState.kHopStart;
        // else
            // do nothing
    }

    void StartHop()
    {
        m_fHopStart = Time.time;
        m_vHopStartPos = transform.position;

        Vector3 vPredictedEndHopPosition = Vector3.zero; // predicted end hop position
        Vector3 vHopDirection = Vector3.zero;            // predicted hop direction

        // general vector direction away from player
        Vector3 vEscapeDirection = (transform.position - m_player.transform.position).normalized;

        for (int i = 0; i < m_nMaxMoveAttempts; i++)
        {
            // randomize angle (reproduce hopping directions)
            float fRandomAngle = Random.Range(-60f, 60f);
            Quaternion qRotation = Quaternion.Euler(0, 0, fRandomAngle);
            vHopDirection = qRotation * vEscapeDirection;

            // predict end position
            vPredictedEndHopPosition = transform.position + (vHopDirection * m_fHopSpeed * m_fHopTime);

            // check if position is on screen
            if (IsOnScreen(vPredictedEndHopPosition))
                break;

            // handle last attempt - could not find a way out in 49 attempts
            // last ditch fallback: run in any direction
            if (i == m_nMaxMoveAttempts - 1)
            {
                for (int j = 0; j < 36; j++) // try 36 directions (every 10 degrees)
                {
                    float randomAngle = Random.Range(0f, 360f);
                    Quaternion randomRotation = Quaternion.Euler(0, 0, randomAngle);
                    vHopDirection = randomRotation * Vector3.right;

                    vPredictedEndHopPosition = transform.position + (vHopDirection * m_fHopSpeed * m_fHopTime);
                    if (IsOnScreen(vPredictedEndHopPosition))
                        break;
                }
            }
        }

        m_vHopEndPos = vPredictedEndHopPosition; // cement final hop position

        // face hop direction
        float fAngle = Mathf.Atan2(vHopDirection.y, vHopDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, fAngle - 90f);

        m_nState = eState.kHop;
    }

    bool IsOnScreen(Vector3 vPredictedEndHopPosition)
    {
        Vector3 screenPos = Camera.main.WorldToViewportPoint(vPredictedEndHopPosition);
        return screenPos.x > 0 && screenPos.x < 1 && screenPos.y > 0 && screenPos.y < 1;
    }

    void PerformHop()
    {
        float elapsed = Time.time - m_fHopStart;
        float progress = elapsed / m_fHopTime;
        if (progress >= 1.0f)
        {
            transform.position = m_vHopEndPos;
            m_nState = eState.kIdle;
        }
        else
        {
            transform.position = Vector3.Lerp(m_vHopStartPos, m_vHopEndPos, progress);
        }
    }
}