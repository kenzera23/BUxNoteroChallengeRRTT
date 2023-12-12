using Notero.Unity.MidiNoteInfo;
using Notero.Utilities.Pooling;
using System.ComponentModel.DataAnnotations;
using UnityEngine;

namespace BU.NineTails.MidiGameplay.Scripts.Gameplay
{
    public class RaindropNote : PoolObject<RaindropNote>
    {
        [SerializeField]
        protected GameObject m_DefaultRaindropGO;

        [SerializeField]
        protected Transform m_leftArchon;

        [SerializeField]
        protected Transform m_rightArchon;

        [SerializeField]
        protected GameObject m_NoteAlphabet;
        
        [SerializeField]
        protected GameObject[] m_SubRaindropEffects;

        [SerializeField]
        protected RectTransform m_NoteRect;

        public float NoteOnTime { get; protected set; }

        public MidiNoteInfo MidiNoteInfo { get; protected set; }

        //Raindrop Sub-Effect indices.
        private const int m_CorrectEffectId = 0;
        private const int m_MissEffectId = 1;

        protected float m_Speed;
        protected float m_Length => (float)MidiNoteInfo.GetNoteDurationInMilliseconds() / 1000f * m_Speed;

        private void Awake()
        {
            AlphabetSymbolSpawner();
        }

        public void Remove()
        {
            Return();
        }

        public void SetDefault()
        {
            m_DefaultRaindropGO.SetActive(true);

            foreach (GameObject effect in m_SubRaindropEffects)
            {
                effect.SetActive(false);
            }
        }

        public virtual void SetCorrect()
        {
            SetFeedbackEffect(true);
        }

        public virtual void SetMiss()
        {
            SetFeedbackEffect(false);
        }

        //TODO: remove if hold on mode is confirm visual
        public void SetHide()
        {
            m_DefaultRaindropGO.SetActive(false);

            foreach (GameObject effect in m_SubRaindropEffects)
            {
                effect.SetActive(false);
            }
        }

        public void Init(float speed, float noteOnTime)
        {
            m_Speed = speed;
            NoteOnTime = noteOnTime;
            SetNoteRectSize();
        }

        public void SetMidiInfo(MidiNoteInfo info)
        {
            MidiNoteInfo = info;
        }

        /// <summary>
        /// Set Note body vertical length.
        /// </summary>
        public virtual void SetNoteRectSize()
        {
            RectTransform rect = (RectTransform)transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_Length);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30);
        }

        public virtual void UpdatePosition(float currentTime)
        {
            currentTime -= NoteOnTime;
            if (m_NoteRect != null)
            {
                float posX = m_NoteRect.anchoredPosition.x - m_Speed * Time.deltaTime;
                Vector2 pos = new Vector2(posX, m_NoteRect.anchoredPosition.y);
                m_NoteRect.anchoredPosition = pos;
            }
        }

        protected virtual void SetFeedbackEffect(bool isCorrect)
        {
            m_SubRaindropEffects[m_CorrectEffectId].SetActive(isCorrect);
            m_SubRaindropEffects[m_MissEffectId].SetActive(!isCorrect);
        }

        public void AlphabetSymbolSpawner()
        {
            Vector3 m_leftArchorPosition = m_leftArchon.position;
            

            GameObject leftObject = Instantiate(m_NoteAlphabet, m_leftArchorPosition, Quaternion.identity);
            

            Destroy(m_NoteAlphabet);
            

            leftObject.transform.localScale = new Vector3(1f, 1f, 1f);
            

            leftObject.transform.SetParent(transform);
            

        }
    }
}