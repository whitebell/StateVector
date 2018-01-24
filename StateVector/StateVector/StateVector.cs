using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace StateVector
{
    public class VectorEventBase
    {
        public string Head { get; set; }
        public string Tail { get; set; }
        public string Tag { get; set; }
        public Action Func { get; set; }
        public int Priority { get; set; } = -1;
        public int Index { get; set; } = -1;

        public VectorEventBase()
        { }

        public VectorEventBase(string head, string tail, Action func)
        {
            Head = head;
            Tail = tail;
            Func = func;
        }

        public VectorEventBase(string head, string tail, string tag, Action func) : this(head, tail, func) => Tag = tag;
    }

    public class VectorState
    {
        protected List<string> m_list = new List<string>();

        public string[] Array => m_list.ToArray();

        public VectorState(params string[] stateList) => m_list.AddRange(stateList);
    }

    public class VectorHead : VectorState
    {
        public VectorHead(params string[] stateList) : base(stateList)
        { }
    }

    public class VectorTail : VectorState
    {
        public VectorTail(params string[] stateList) : base(stateList)
        { }
    }

    public class VectorEvent
    {
        protected List<VectorEventBase> m_vectorEventList = new List<VectorEventBase>();

        public VectorEventBase[] Array => m_vectorEventList.ToArray();

        public VectorEvent()
        { }

        public VectorEvent(string head, string tail, params Action[] funcArray) => Init(head, tail, "", funcArray);

        public VectorEvent(VectorHead head, string tail, params Action[] funcArray) => Init(head.Array, tail, "", funcArray);

        public VectorEvent(string head, VectorTail tail, params Action[] funcArray) => Init(head, tail.Array, "", funcArray);

        public VectorEvent(VectorHead head, VectorTail tail, params Action[] funcArray) => Init(head.Array, tail.Array, "", funcArray);

        public VectorEvent(string head, string tail, string tag, params Action[] funcArray) => Init(head, tail, tag, funcArray);

        public VectorEvent(VectorHead head, string tail, string tag, params Action[] funcArray) => Init(head.Array, tail, tag, funcArray);

        public VectorEvent(string head, VectorTail tail, string tag, params Action[] funcArray) => Init(head, tail.Array, tag, funcArray);

        public VectorEvent(VectorHead head, VectorTail tail, string tag, params Action[] funcArray) => Init(head.Array, tail.Array, tag, funcArray);

        public static VectorHead HeadOr(params string[] head) => new VectorHead(head);

        public static VectorTail TailOr(params string[] tail) => new VectorTail(tail);

        public static Action Func(Action func) => func;

        public static Action[] FuncArray(params Action[] funcArray) => funcArray;

        protected void Init(string[] headArray, string[] tailArray, string tag, params Action[] funcArray)
        {
            foreach (var head in headArray)
            {
                if (String.IsNullOrEmpty(head))
                    throw new ArgumentException("head array contains \"\"");

                Init(head, tailArray, tag, funcArray);
            }
        }

        protected void Init(string[] headArray, string tail, string tag, params Action[] funcArray)
        {
            foreach (var head in headArray)
            {
                if (String.IsNullOrEmpty(head))
                    throw new ArgumentException("head array contains \"\"");

                Init(head, tail, tag, funcArray);
            }
        }

        protected void Init(string head, string[] tailArray, string tag, params Action[] funcArray)
        {
            foreach (var tail in tailArray)
            {
                if (String.IsNullOrEmpty(tail))
                {
                    throw new ArgumentException("tail array contains \"\"");
                }

                Init(head, tail, tag, funcArray);
            }
        }

        protected void Init(string head, string tail, string tag, params Action[] funcArray)
        {
            if (head == null)
                throw new ArgumentNullException(nameof(head));
            if (tail == null)
                throw new ArgumentNullException(nameof(tail));
            if (tag == null)
                throw new ArgumentNullException(nameof(tag));

            foreach (var func in funcArray)
            {
                if (func == null)
                    throw new ArgumentNullException();

                m_vectorEventList.Add(new VectorEventBase(head, tail, tag, func));
            }
        }
    }

    /// <summary>
    /// 本クラスは関数テーブルの使い勝手向上を目的とするクラスである。
    /// コンストラクタで状態変化時に実行する処理を集約して登録する。
    ///
    /// 関数テーブルでは、組み合わせ増加に応じてテーブルサイズや次元数が増加する。
    /// しかし、実際に使用される状態変化条件は僅かである。
    /// また、条件変更時の書き換え作業が煩雑になる傾向がある。
    ///
    /// 動的な関数テーブル変更は非推奨。
    /// GUIの状態変化を想定しているため、
    /// 10ミリ秒単位の精度が要求される高速な状態変化の制御には不適切。
    /// </summary>
    public class StateVector
    {
        public bool EnableRefreshTrace;
        public bool EnableRegexp;
        protected List<VectorEventBase> m_eventList = new List<VectorEventBase>();

        public string StateNow { get; set; }
        public string StateOld { get; private set; }

        public string ListName { get; set; }

        public StateVector()
        { }

        public StateVector(string startState, VectorEvent[] eventArray)
        {
            int index = 0;
            int prioroty = 0;
            StateNow = startState;
            m_eventList.Clear();

            foreach (var ve in eventArray)
            {
                foreach (var ins in ve.Array)
                {
                    ins.Index = index;
                    ins.Priority = prioroty;
                    m_eventList.Add(ins);
                    prioroty++;
                }
                index++;
            }
        }

        public StateVector(string listName, string startState, VectorEvent[] eventArray) : this(startState, eventArray) => ListName = listName;

        public void GetListInfo()
        {
            foreach (var ins in m_eventList)
                Debug.WriteLine($"{ListName}:{ins.Tag} list[{ins.Index}].priority({ins.Priority}) {ins.Head} -> {ins.Tail} , {ins.Func.Method.Name}");
        }

        public void Refresh(string stateNext)
        {
            var list = new List<VectorEventBase>();

            if (EnableRegexp)
                list = GetRegexp(StateNow, stateNext);
            else
                list = GetHeadAndTali(StateNow, stateNext);

            foreach (var ins in list)
            {
                if (EnableRefreshTrace)
                {
                    Debug.Write(ListName + " " + ins.Tag + " ");
                    Debug.Write(StateNow + " -> " + stateNext);
                    Debug.Write(" do[" + ins.Index + "].priority(" + ins.Priority + ") " + ins.Func.Method.Name);
                }

                ins.Func();

                if (EnableRefreshTrace)
                {
                    Debug.WriteLine(" done.");
                }
            }

            StateOld = StateNow;
            StateNow = stateNext;
        }

        protected List<VectorEventBase> GetHeadAndTali(string stateNow, string stateNext)
        {
            var ret = new List<VectorEventBase>();

            foreach (var ins in m_eventList)
            {
                if (stateNow == ins.Head && stateNext == ins.Tail)
                    ret.Add(ins);
            }

            return ret;
        }

        protected List<VectorEventBase> GetRegexp(string stateNow, string stateNext)
        {
            var ret = new List<VectorEventBase>();

            foreach (var ins in m_eventList)
            {
                if (Regex.IsMatch(stateNow, ins.Head) && Regex.IsMatch(stateNext, ins.Tail))
                    ret.Add(ins);
            }

            return ret;
        }
    }
}
