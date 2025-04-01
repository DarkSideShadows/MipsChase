using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

public class Player : MonoBehaviour
{
    // External tunables.
    static public float m_fMaxSpeed = 0.10f;
    public float m_fSlowSpeed = m_fMaxSpeed * 0.3f;
    public float m_fIncSpeed = 0.0025f;
    public float m_fMagnitudeFast = 0.3f;
    public float m_fMagnitudeSlow = 0.06f;
    public float m_fFastRotateSpeed = 0.2f; // turn speed, u/s (kMoveFast)
    public float m_fFastRotateMax = 10.0f;  // turn angle threshold (kMoveFast)
    public float m_fDiveTime = 0.3f;
    public float m_fDiveRecoveryTime = 0.5f;
    public float m_fDiveDistance = 3.0f;

    // Internal variables.
    public float m_fAngle;          // current angle (to mouse)
    public float m_fSpeed;          // current speed
    public float m_fTargetSpeed;    // desired speed (m_fSlowSpeed or decaying m_fMaxSpeed)
    public float m_fTargetAngle;    // desired move angle (to mouse)
    public eState m_nState;         // current player state
    public Vector3 m_vDiveStartPos; // marker for dive start position
    public Vector3 m_vDiveEndPos;   // calculation for dive end position
    public float m_fDiveStartTime;  // marker for dive timer

    // additional variables
    public float m_fSpeedDecayRate = 0.4f;// when moving fast, for decaying move speed
    public float m_fTimer = 0f;           // timer for slow->fast or fast->slow move transitions
    static public float m_fBuffer = 0.1f; // time delay for transitions (cannot switch immediately)
    static public Vector3 m_vInitialDirection = Vector3.left;

    public enum eState : int
    {
        kMoveSlow,
        kMoveFast,
        kDiving,
        kRecovering,
        kNumStates
    }

    private Color[] stateColors = new Color[(int)eState.kNumStates]
    {
        new Color(0,     0,   0),
        new Color(255, 255, 255),
        new Color(0,     0, 255),
        new Color(0,   255,   0),
    };

    public bool IsDiving()
    {
        return (m_nState == eState.kDiving);
    }

    void CheckForDive()
    {
        if (Input.GetMouseButton(0) && (m_nState != eState.kDiving && m_nState != eState.kRecovering))
        {
            // Start the dive operation
            m_nState = eState.kDiving;
            m_fSpeed = 0.0f;

            // Store starting parameters.
            m_vDiveStartPos = transform.position;
            m_vDiveEndPos = m_vDiveStartPos - (transform.right * m_fDiveDistance);
            m_fDiveStartTime = Time.time;
        }
    }

    void Start()
    {
        // Initialize variables.
        m_fAngle = 0;
        m_fSpeed = 0;
        m_nState = eState.kMoveSlow;

        // after playtesting, this is the value i got to make fast turning work. 
        // the external tunable (at the start of the file) represents 20% 
        // while the raw speed value is an arbitrary value: 200f - this works to dampen rotation
        m_fFastRotateSpeed *= 1000f; 
    }

    void UpdateDirectionAndSpeed()
    {
        // Get relative positions between the mouse and player
        Vector3 vScreenPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 vScreenSize = Camera.main.ScreenToWorldPoint(new Vector2(Screen.width, Screen.height));
        Vector2 vOffset = new Vector2(transform.position.x - vScreenPos.x, transform.position.y - vScreenPos.y);

        // Find the target angle being requested.
        m_fTargetAngle = Mathf.Atan2(vOffset.y, vOffset.x) * Mathf.Rad2Deg;

        // Calculate how far away from the player the mouse is.
        float fMouseMagnitude = vOffset.magnitude / vScreenSize.magnitude;

        // Based on distance, calculate the speed the player is requesting.
        if (fMouseMagnitude > m_fMagnitudeFast)
        {
            m_fTargetSpeed = m_fMaxSpeed;
        }
        else if (fMouseMagnitude > m_fMagnitudeSlow)
        {
            m_fTargetSpeed = m_fSlowSpeed;
        }
        else
        {
            m_fTargetSpeed = 0.0f;
        }
    }

    void FixedUpdate()
    {
        GetComponent<Renderer>().material.color = stateColors[(int)m_nState];
    }

    /*
    The player should behave in the following fashion:

    - Moving slowly until the speed reaches the fast threshold. While moving slowly the angle of the player can change immediately and a dive can be initiated. Once the speed in a specific direction has increased past the threshold, the player will begin moving quickly.
    - Moving quickly the player cannot turn immediately outside of a small threshold. If the player moves the mouse outside that directional range, the player will continue in the original direction but begin slowing down. When the player gets below the speed threshold it will drop back to moving slowly.
    - Dive when the left mouse button is pressed. Similar to hop this should be a visible but quick movement. This is the only state when Mips can be caught.
    - Recovery afterwards where no movement is possible, followed by transitioning to the slow move state.
     */

    void Update()
    {
       // ensure player cannot leave screen
       Vector3 clampedViewPos = Camera.main.WorldToViewportPoint(transform.position);
       clampedViewPos.x = Mathf.Clamp01(clampedViewPos.x);
       clampedViewPos.y = Mathf.Clamp01(clampedViewPos.y);
       transform.position = Camera.main.ViewportToWorldPoint(clampedViewPos);

       // each player state has its own logic for moving the player
       switch (m_nState)
       {
           case eState.kMoveSlow:
               HandleMoveSlow(); // m_fSlowSpeed
               break;
           case eState.kMoveFast:
               HandleMoveFast(); // m_fFastSpeed
               break;
           case eState.kDiving:
               HandleDiving(); // Lerp based on diveProgress
               break;
           case eState.kRecovering:
               HandleRecovering(); // no movement
               break;
       }
    }

    void HandleMoveSlow()
    {
       // start here
       // adjust timer first - this timer holds the MoveSlow state immediately after switching from MoveFast, preventing MoveFast->MoveFast transitions
       if (m_fTimer >= 0f)
       {
           m_fTimer -= Time.deltaTime;
       }
       CheckForDive();             // can initiate dive
       UpdateDirectionAndSpeed();  // adjust m_fSpeed according to mouse distance

       m_fAngle = m_fTargetAngle; // immediate rotation toward mouse
       m_fSpeed = m_fTargetSpeed; // set player speed

       // slow -> fast transition logic
       if (m_fTimer <= 0f && m_fSpeed >= m_fMaxSpeed)
       {
           m_nState = eState.kMoveFast;
       }

       // move and rotate player
       if (m_fSpeed <= m_fSlowSpeed)
       {
           Vector3 direction = Quaternion.Euler(0, 0, m_fAngle) * m_vInitialDirection;
           transform.position += direction * m_fSpeed;
           transform.rotation = Quaternion.Euler(0, 0, m_fAngle);
       }
    }

    void HandleMoveFast()
    {
       CheckForDive();             // can initiate dive
       UpdateDirectionAndSpeed();  // adjust speed and position

       float angleDelta = Mathf.DeltaAngle(m_fAngle, m_fTargetAngle); // desired vs current player angle
       Vector3 direction;

       // handle rotation & decaying speed values
       if (Mathf.Abs(angleDelta) > m_fFastRotateMax) // sharp angle above threshold - slow down
       {
           m_fSpeed = Mathf.Max(m_fSpeed - m_fSpeedDecayRate * Time.deltaTime, 0f); // gradually decelerate
       }
       else // within small angle threshold - rotate according m_fAngle with m_fFastRotateSpeed
       {
           float rotationStep = m_fFastRotateSpeed * Time.deltaTime;
           m_fAngle += Mathf.Sign(angleDelta) * rotationStep;
       }

       // move and rotate player
       direction = Quaternion.Euler(0, 0, m_fAngle) * m_vInitialDirection;
       transform.position += direction * m_fSpeed;
       transform.rotation = Quaternion.Euler(0, 0, m_fAngle);

       // fast -> slow transition logic
       if (m_fSpeed <= m_fSlowSpeed)
       {
           m_fTimer = m_fBuffer;        // reset timer before switching from fast -> slow
           m_fSpeed = m_fSlowSpeed;     // set speed
           m_nState = eState.kMoveSlow; // move slowly again
       }
    }

    void HandleDiving()
    {
       // transition to recovering
       float elapsed = Time.time - m_fDiveStartTime;
       float diveProgress = elapsed / m_fDiveTime;
       if (diveProgress >= 1f)
       {
           transform.position = m_vDiveEndPos;
           m_nState = eState.kRecovering;
           return;
       }

       // move player (commence dive)
       transform.position = Vector3.Lerp(m_vDiveStartPos, m_vDiveEndPos, diveProgress);
    }

    void HandleRecovering()
    {
       // transition to move slow
       float elapsed = Time.time - m_fDiveStartTime;
       if (elapsed > m_fDiveTime + m_fDiveRecoveryTime)
           m_nState = eState.kMoveSlow;
    }
}