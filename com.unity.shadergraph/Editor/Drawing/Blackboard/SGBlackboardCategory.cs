using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing
{
    class SGBlackboardCategory : GraphElement, ISGControlledElement<BlackboardCategoryController>
    {
        // --- Begin ISGControlledElement implementation
        public void OnControllerChanged(ref SGControllerChangedEvent e)
        {
        }

        public void OnControllerEvent(SGControllerEvent e)
        {
        }

        public BlackboardCategoryController controller
        {
            get => m_Controller;
            set
            {
                if (m_Controller != value)
                {
                    if (m_Controller != null)
                    {
                        m_Controller.UnregisterHandler(this);
                    }
                    m_Controller = value;

                    if (m_Controller != null)
                    {
                        m_Controller.RegisterHandler(this);
                    }
                }
            }
        }

        SGController ISGControlledElement.controller => m_Controller;

        // --- ISGControlledElement implementation

        BlackboardCategoryController m_Controller;

        BlackboardCategoryViewModel m_ViewModel;

        VisualElement m_DragIndicator;
        VisualElement m_MainContainer;
        VisualElement m_Header;
        Label m_TitleLabel;
        TextField m_TextField;
        internal TextField textField => m_TextField;
        VisualElement m_RowsContainer;
        int m_InsertIndex;
        SGBlackboard Blackboard => m_ViewModel.parentView as SGBlackboard;

        public delegate bool CanAcceptDropDelegate(ISelectable selected);

        public CanAcceptDropDelegate canAcceptDrop { get; set; }

        int InsertionIndex(Vector2 pos)
        {
            int index = -1;
            VisualElement owner = contentContainer != null ? contentContainer : this;
            Vector2 localPos = this.ChangeCoordinatesTo(owner, pos);

            if (owner.ContainsPoint(localPos))
            {
                index = 0;

                foreach (VisualElement child in Children())
                {
                    Rect rect = child.layout;

                    if (localPos.y > (rect.y + rect.height / 2))
                    {
                        ++index;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return index;
        }

        VisualElement FindCategoryDirectChild(VisualElement element)
        {
            VisualElement directChild = element;

            while ((directChild != null) && (directChild != this))
            {
                if (directChild.parent == this)
                {
                    return directChild;
                }
                directChild = directChild.parent;
            }

            return null;
        }

        internal SGBlackboardCategory(BlackboardCategoryViewModel categoryViewModel)
        {
            m_ViewModel = categoryViewModel;

            // Setup VisualElement from Stylesheet and UXML file
            var tpl = Resources.Load("UXML/GraphView/BlackboardSection") as VisualTreeAsset;
            m_MainContainer = tpl.Instantiate();
            m_MainContainer.AddToClassList("mainContainer");

            m_Header = m_MainContainer.Q("sectionHeader");
            m_TitleLabel = m_MainContainer.Q<Label>("sectionTitleLabel");
            m_RowsContainer = m_MainContainer.Q("rowsContainer");
            m_TextField = m_MainContainer.Q<TextField>("textField");
            m_TextField.style.display = DisplayStyle.None;

            hierarchy.Add(m_MainContainer);

            m_DragIndicator = m_MainContainer.Q("dragIndicator");
            m_DragIndicator.visible = false;

            hierarchy.Add(m_DragIndicator);

            ClearClassList();
            AddToClassList("blackboardSection");
            RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
            capabilities |= Capabilities.Selectable | Capabilities.Droppable | Capabilities.Deletable | Capabilities.Renamable;

            //add right click context menu
            this.AddManipulator(new SelectionDropper());
            this.AddManipulator(new ContextualMenuManipulator(BuildFieldContextualMenu));

            var textInputElement = m_TextField.Q(TextField.textInputUssName);
            textInputElement.RegisterCallback<FocusOutEvent>(e => { OnEditTextFinished(); });

            RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
            RegisterCallback<DragPerformEvent>(OnDragPerformEvent);
            RegisterCallback<DragLeaveEvent>(OnDragLeaveEvent);

            var styleSheet = Resources.Load<StyleSheet>($"Styles/Blackboard");
            styleSheets.Add(styleSheet);

            m_InsertIndex = -1;

            // Update category title from view model
            m_TitleLabel.text = m_ViewModel.name;
        }

        public override VisualElement contentContainer { get { return m_RowsContainer; } }

        public override string title
        {
            get { return m_TitleLabel.text; }
            set { m_TitleLabel.text = value; }
        }

        public bool headerVisible
        {
            get { return m_Header.parent != null; }
            set
            {
                if (value == (m_Header.parent != null))
                    return;

                if (value)
                {
                    m_MainContainer.Add(m_Header);
                }
                else
                {
                    m_MainContainer.Remove(m_Header);
                }
            }
        }

        private void SetDragIndicatorVisible(bool visible)
        {
            m_DragIndicator.visible = visible;
        }

        public bool CanAcceptDrop(List<ISelectable> selection)
        {
            if (selection == null)
                return false;

            // Look for at least one selected element in this category to accept drop
            foreach (ISelectable selected in selection)
            {
                VisualElement selectedElement = selected as VisualElement;

                if (selected != null && Contains(selectedElement))
                {
                    if (canAcceptDrop == null || canAcceptDrop(selected))
                        return true;
                }
            }

            return false;
        }

        private void OnDragUpdatedEvent(DragUpdatedEvent evt)
        {
            var selection = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;

            if (!CanAcceptDrop(selection))
            {
                SetDragIndicatorVisible(false);
                return;
            }

            VisualElement sourceItem = null;

            foreach (ISelectable selectedElement in selection)
            {
                sourceItem = selectedElement as VisualElement;

                if (sourceItem == null)
                    continue;
            }

            if (!Contains(sourceItem))
            {
                SetDragIndicatorVisible(false);
                return;
            }

            Vector2 localPosition = evt.localMousePosition;

            m_InsertIndex = InsertionIndex(localPosition);

            if (m_InsertIndex != -1)
            {
                float indicatorY = 0;

                if (m_InsertIndex == childCount)
                {
                    VisualElement lastChild = this[childCount - 1];

                    indicatorY = lastChild.ChangeCoordinatesTo(this, new Vector2(0, lastChild.layout.height + lastChild.resolvedStyle.marginBottom)).y;
                }
                else
                {
                    VisualElement childAtInsertIndex = this[m_InsertIndex];

                    indicatorY = childAtInsertIndex.ChangeCoordinatesTo(this, new Vector2(0, -childAtInsertIndex.resolvedStyle.marginTop)).y;
                }

                SetDragIndicatorVisible(true);

                m_DragIndicator.style.width = layout.width;
                var newPosition = indicatorY - m_DragIndicator.layout.height / 2;
                m_DragIndicator.style.top = newPosition;
            }
            else
            {
                SetDragIndicatorVisible(false);

                m_InsertIndex = -1;
            }

            if (m_InsertIndex != -1)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }

            evt.StopPropagation();
        }

        private void OnDragPerformEvent(DragPerformEvent evt)
        {
            var selection = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;

            if (!CanAcceptDrop(selection))
            {
                SetDragIndicatorVisible(false);
                return;
            }

            if (m_InsertIndex != -1)
            {
                List<Tuple<VisualElement, VisualElement>> draggedElements = new List<Tuple<VisualElement, VisualElement>>();

                foreach (ISelectable selectedElement in selection)
                {
                    var draggedElement = selectedElement as VisualElement;

                    if (draggedElement != null && Contains(draggedElement))
                    {
                        draggedElements.Add(new Tuple<VisualElement, VisualElement>(FindCategoryDirectChild(draggedElement), draggedElement));
                    }
                }

                if (draggedElements.Count == 0)
                {
                    SetDragIndicatorVisible(false);
                    return;
                }

                // Sorts the dragged elements from their relative order in their parent
                draggedElements.Sort((pair1, pair2) => { return IndexOf(pair1.Item1).CompareTo(IndexOf(pair2.Item1)); });

                int insertIndex = m_InsertIndex;

                foreach (Tuple<VisualElement, VisualElement> draggedElement in draggedElements)
                {
                    VisualElement categoryDirectChild = draggedElement.Item1;
                    int indexOfDraggedElement = IndexOf(categoryDirectChild);

                    if (!((indexOfDraggedElement == insertIndex) || ((insertIndex - 1) == indexOfDraggedElement)))
                    {
                        var moveShaderInputAction = new MoveShaderInputAction();
                        if (draggedElement.Item2.userData is ShaderInput shaderInput)
                        {
                            moveShaderInputAction.shaderInputReference = shaderInput;
                            moveShaderInputAction.newIndexValue = m_InsertIndex;
                            m_ViewModel.requestModelChangeAction(moveShaderInputAction);

                            if (insertIndex == contentContainer.childCount)
                            {
                                categoryDirectChild.BringToFront();
                            }
                            else
                            {
                                categoryDirectChild.PlaceBehind(this[insertIndex]);
                            }
                        }
                    }

                    if (insertIndex > indexOfDraggedElement) // No need to increment the insert index for the next dragged element if the current dragged element is above the current insert location.
                        continue;
                    insertIndex++;
                }
            }

            SetDragIndicatorVisible(false);
            evt.StopPropagation();
        }

        void OnMouseDownEvent(MouseDownEvent e)
        {
            if ((e.clickCount == 2) && e.button == (int)MouseButton.LeftMouse && IsRenamable())
            {
                OpenTextEditor();
                e.PreventDefault();
            }
        }

        protected virtual void BuildFieldContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", (a) => OpenTextEditor(), DropdownMenuAction.AlwaysEnabled);
        }

        internal void OpenTextEditor()
        {
            m_TextField.SetValueWithoutNotify(title);
            m_TextField.style.display = DisplayStyle.Flex;
            m_TitleLabel.visible = false;
            m_TextField.Q(TextField.textInputUssName).Focus();
            m_TextField.SelectAll();
        }

        void OnEditTextFinished()
        {
            m_TitleLabel.visible = true;
            m_TextField.style.display = DisplayStyle.None;

            if (title != m_TextField.text && String.IsNullOrWhiteSpace(m_TextField.text) == false && String.IsNullOrEmpty(m_TextField.text) == false)
            {
                var changeCategoryNameAction = new ChangeCategoryNameAction();
                changeCategoryNameAction.newCategoryNameValue = m_TextField.text;
                changeCategoryNameAction.categoryGuid = m_ViewModel.associatedCategoryGuid;
                m_ViewModel.requestModelChangeAction(changeCategoryNameAction);
            }
            else
            {
                // Reset text field to original name
                m_TextField.value = title;
            }
        }

        void OnDragLeaveEvent(DragLeaveEvent evt)
        {
            SetDragIndicatorVisible(false);
        }

        internal void OnDragActionCanceled()
        {
            SetDragIndicatorVisible(false);
        }
    }
}
