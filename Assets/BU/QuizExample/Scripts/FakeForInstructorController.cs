using BU.QuizExample.QuizExampleMessages;
using BU.QuizExample.QuizExampleMessages.EventHandler;
using BU.QuizExample.Scripts.UI;
using DataStore;
using DataStore.Quiz;
using Mirror;
using Notero.QuizConnector;
using Notero.Unity.Networking.Mirror;
using Notero.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BU.QuizExample.Scripts
{
    public class FakeForInstructorController : MonoSingleton<FakeForInstructorController>
    {
        private IQuizController m_CurrentQuizController;

        private QuizStates CurrentQuizState { get; set; }

        private QuizModes QuizMode { get; set; }

        private QuizControllerType m_QuizControllerType { get; set; }

        private QuizStore m_QuizStore => Store.QuizStore;

        private HUDController m_HUDController;
        private IEventHandler[] m_EventHandler;
        private EventBus m_EventBus;
        private Store Store;
        private string m_JsonContent;

        private const int StudentAmount = 4;

        public void Init(QuizControllerType quizControllerType, string jsonContent, HUDController hudController)
        {
            Store = Store.Default;
            m_EventBus = EventBus.Default;
            m_QuizControllerType = quizControllerType;
            m_JsonContent = jsonContent;
            m_HUDController = hudController;

            m_EventHandler = new IEventHandler[] { new InstructorQuizEventHandler(m_QuizStore) };

            m_HUDController.OnNextClick.AddListener(GoToNextState);

            SubscribeInstructorEvent();
        }

        private void OnDisable()
        {
            UnsubscribeInstructorEvent();

            m_HUDController.OnNextClick.RemoveAllListeners();
        }

        public void DestroyCurrentUI()
        {
            Destroy(QuizConnectorController.Instance.CurrentUI);
        }

        public void SetQuizMode(QuizModes quizMode) => QuizMode = quizMode;

        #region Send Event Methods

        private IEnumerator SetStudentAnswer(Action onFinish)
        {
            var questionId = m_QuizStore.CurrentQuestion.Id;

            if(!m_QuizStore.StudentAnswers.TryGetValue(questionId, out var studentAnswers))
            {
                onFinish?.Invoke();
                yield break;
            }

            foreach(var answer in studentAnswers)
            {
                var delay = Random.Range(1, 3);

                yield return new WaitForSeconds(delay);
                SendStudentAnswerToInstructor(answer.StationId, Random.Range(1, 5).ToString());

                onFinish?.Invoke();
            }
        }

        private void SendStudentAnswerToInstructor(string stationId, string answer)
        {
            if(m_EventBus == null) return;
            m_EventBus.Publish(new LocalConnectionToClient(), new StudentAnswerMessage
            {
                StationId = stationId,
                Answer = answer
            });
        }

        #endregion

        #region Receive Event Methods

        private void SubscribeInstructorEvent()
        {
            foreach(var eventHandler in m_EventHandler) eventHandler.Subscribe(m_EventBus);

            m_EventBus.Subscribe<StudentAnswerMessage>(OnStudentAnswerReceived);
            m_EventBus.Subscribe<CustomDataMessage>(OnCustomDataReceived);
        }

        private void UnsubscribeInstructorEvent()
        {
            m_EventBus.Unsubscribe<StudentAnswerMessage>(OnStudentAnswerReceived);
            m_EventBus.Unsubscribe<CustomDataMessage>(OnCustomDataReceived);

            foreach(var eventHandler in m_EventHandler) eventHandler.Unsubscribe(m_EventBus);
        }

        private void OnStudentAnswerReceived(NetworkConnectionToClient connection, StudentAnswerMessage message)
        {
            QuizConnectorController.Instance.OnStudentAnswerReceive(message.StationId, message.Answer, m_QuizStore.AnswerStudentAmount, m_QuizStore.StudentAmount);
        }

        private void OnCustomDataReceived(NetworkConnectionToClient connection, CustomDataMessage message)
        {
            QuizConnectorController.Instance.OnCustomDataMessageReceive(message.Data);
        }

        #endregion

        private void OnCustomDataReceive(byte[] data)
        {
            m_EventBus.Publish(new CustomDataMessage() { Data = data });
        }

        private void GoToNextState()
        {
            m_HUDController.gameObject.SetActive(false);
            QuizConnectorController.Instance.DestroyInstructorQuestionStateUI();
            QuizConnectorController.Instance.DestroyInstructorPreResultStateUI();

            var correctAnswer = QuizState.Default.CurrentQuestion.Answer.CorrectAnswers.ElementAt(0);
            m_QuizStore.SetCorrectAnswer(correctAnswer);

            if(QuizMode == QuizModes.POP_QUIZ && CurrentQuizState != QuizStates.PRERESULT)
            {
                StopSetStudentAnswerCoroutine();
                PreResultState();
            }
            else
            {
                PreparingState();
            }
        }

        #region StarterState

        public void StarterState()
        {
            QuizConnectorController.Instance.Init(m_QuizStore, "A");
            QuizConnectorController.Instance.SetChapterIndex(1);
            QuizConnectorController.Instance.SetRootDirectory("");
            QuizConnectorController.Instance.SetChapter("Chapter");
            QuizConnectorController.Instance.SetMission("Mission");
            QuizConnectorController.Instance.SetGenerateTextureLogic((path, rootDir) =>
            {
                path = Path.Combine(rootDir, path);

                var imagePath = path.Split(".")[0].Trim();
                var texturePath = Path.Combine(imagePath);
                var resource = Resources.Load<Texture2D>(texturePath);

                resource.filterMode = FilterMode.Bilinear;
                resource.anisoLevel = 4;

                return resource;
            });

            QuizConnectorController.Instance.OnCustomDataReceive.AddListener(OnCustomDataReceive);
            QuizConnectorController.Instance.OnNextStateReceive.AddListener(GoToNextState);

            LoadingState();
        }

        #endregion

        #region LoadingState

        private void LoadingState()
        {
            CurrentQuizState = QuizStates.LOADING;
            QuizConnectorController.Instance.LoadQuizToQuizStore(m_JsonContent);

            if(m_QuizControllerType == QuizControllerType.FLOW)
            {
                PreparingState();
            }
            else if(m_QuizControllerType == QuizControllerType.RESULT)
            {
                // [instructor] Set quiz store
                var questionAmount = QuizState.Default.QuestionAmount;
                m_QuizStore.SetQuizInfo(new QuizInfo(questionAmount, questionAmount));

                // [instructor] Mock result data of student side
                var studentScore = new Dictionary<string, int>();

                foreach(var question in m_QuizStore.QuizList.Values)
                {
                    m_QuizStore.SetCurrentQuestionById(question.Id);

                    for(int i = 1; i <= StudentAmount; i++)
                    {
                        var randomScore = Random.Range(0, m_QuizStore.QuizInfo.QuestionAmount + 1);
                        var randomAnswer = Random.Range(1, 5);
                        var stationId = i.ToString();

                        studentScore.TryAdd(stationId, randomScore);
                        m_QuizStore.SetStudentAnswer(stationId, randomAnswer.ToString());
                    }
                }

                m_QuizStore.SetStudentPreTestResult(studentScore);

                ResultState();
            }
        }

        #endregion

        #region PreparingState

        private void PreparingState()
        {
            CurrentQuizState = QuizStates.PREPARING;

            var quizState = QuizState.Default;

            if(quizState.GetNextQuestionIfExist(out var quiz))
            {
                m_QuizStore.SetQuizInfo(new QuizInfo(quizState.CurrentQuestionNumber, quizState.QuestionAmount));
                m_QuizStore.SetCurrentQuestionById(quiz.Id);

                for(int i = 1; i <= 4; i++) m_QuizStore.AddStudentAnswer(i.ToString());

                CountInState();
            }
            else
            {
                EndState();
            }
        }

        #endregion

        #region CountInState

        private void CountInState()
        {
            CurrentQuizState = QuizStates.COUNTIN;

            QuizConnectorController.Instance.SpawnInstructorCountInStateUI();
            QuizConnectorController.Instance.AddInstructorFinishCountInStateEventListener(OnInstructorCountInFinish);
        }

        private void OnInstructorCountInFinish()
        {
            QuizConnectorController.Instance.RemoveInstructorFinishCountInStateEventListener(OnInstructorCountInFinish);
            QuizConnectorController.Instance.DestroyInstructorCountInStateUI();
            QuestionState();
        }

        #endregion

        #region QuestionState

        private Coroutine m_SetStudentAnswerCoroutine;

        private void QuestionState()
        {
            CurrentQuizState = QuizStates.QUESTION;

            m_HUDController.gameObject.SetActive(true);
            QuizConnectorController.Instance.SpawnInstructorQuestionStateUI();

            // Have only in fake
            StartSetStudentAnswerCoroutine();
        }

        private void StartSetStudentAnswerCoroutine()
        {
            if(m_SetStudentAnswerCoroutine != null) return;

            m_SetStudentAnswerCoroutine = CoroutineHelper.Instance.StartCoroutine(SetStudentAnswer(() => { m_SetStudentAnswerCoroutine = null; }));
        }

        private void StopSetStudentAnswerCoroutine()
        {
            if(m_SetStudentAnswerCoroutine == null) return;

            CoroutineHelper.Instance.StopCoroutine(m_SetStudentAnswerCoroutine);
            m_SetStudentAnswerCoroutine = null;
        }

        #endregion

        #region PreresultState

        private void PreResultState()
        {
            CurrentQuizState = QuizStates.PRERESULT;

            m_HUDController.gameObject.SetActive(true);
            QuizConnectorController.Instance.SpawnInstructorPreResultStateUI();
        }

        #endregion

        #region instructor EndState

        private void EndState()
        {
            CurrentQuizState = QuizStates.END;

            QuizConnectorController.Instance.SpawnInstructorEndStateUI();
        }

        #endregion

        #region instructor Result state

        private void ResultState()
        {
            QuizConnectorController.Instance.SpawnInstructorResultStateUI(QuizMode.ToString());
        }

        #endregion
    }
}
