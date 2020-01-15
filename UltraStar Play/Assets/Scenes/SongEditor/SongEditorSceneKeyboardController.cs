﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniInject;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#pragma warning disable CS0649

public class SongEditorSceneKeyboardController : MonoBehaviour, INeedInjection
{

    [Inject(searchMethod = SearchMethods.FindObjectOfType)]
    private SongEditorSceneController songEditorSceneController;

    [Inject(searchMethod = SearchMethods.FindObjectOfType)]
    private NoteArea noteArea;

    [Inject]
    private SongEditorSelectionController selectionController;

    [Inject]
    private EditorNoteDisplayer editorNoteDisplayer;

    [Inject]
    private SongEditorHistoryManager historyManager;

    [Inject]
    private Settings settings;

    [Inject]
    private SongMeta songMeta;

    [Inject]
    private DeleteNotesAction deleteNotesAction;

    [Inject]
    private SongAudioPlayer songAudioPlayer;

    [Inject]
    private EventSystem eventSystem;

    public void Update()
    {
        if (GameObjectUtils.InputFieldHasFocus(eventSystem))
        {
            return;
        }

        EKeyboardModifier modifier = InputUtils.GetCurrentKeyboardModifier();

        // Play / pause via Space
        if (Input.GetKeyUp(KeyCode.Space))
        {
            ToggleAudioPlayPause();
        }

        // Stop via Escape
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            songAudioPlayer.PauseAudio();
        }

        // Select all notes via Ctrl+A
        if (Input.GetKeyUp(KeyCode.A) && modifier == EKeyboardModifier.Ctrl)
        {
            selectionController.SelectAll();
        }

        // Delete notes
        if (Input.GetKeyUp(KeyCode.Delete))
        {
            List<Note> selectedNotes = selectionController.GetSelectedNotes();
            deleteNotesAction.ExecuteAndNotify(selectedNotes);
        }

        // Undo via Ctrl+Z
        if (Input.GetKeyUp(KeyCode.Z) && modifier == EKeyboardModifier.Ctrl)
        {
            historyManager.Undo();
        }

        // Redo via Ctrl+Y
        if (Input.GetKeyUp(KeyCode.Y) && modifier == EKeyboardModifier.Ctrl)
        {
            historyManager.Redo();
        }

        // Save via Ctrl+S
        if (Input.GetKeyUp(KeyCode.S) && modifier == EKeyboardModifier.Ctrl)
        {
            songEditorSceneController.SaveSong();
        }

        // Tab to select next note, Shift+Tab to select previous note
        if (Input.GetKeyUp(KeyCode.Tab))
        {
            if (modifier == EKeyboardModifier.None)
            {
                selectionController.SelectNextNote();
            }
            else if (modifier == EKeyboardModifier.Shift)
            {
                selectionController.SelectPreviousNote();
            }
        }

        // Start editing of lyrics with F2
        if (Input.GetKeyUp(KeyCode.F2))
        {
            List<Note> selectedNotes = selectionController.GetSelectedNotes();
            if (selectedNotes.Count == 1)
            {
                Note selectedNote = selectedNotes.FirstOrDefault();
                EditorUiNote uiNote = editorNoteDisplayer.GetUiNoteForNote(selectedNote);
                if (uiNote != null)
                {
                    uiNote.StartEditingNoteText();
                }
            }
        }

        // Change position in song with Ctrl+ArrowKey
        if (!songAudioPlayer.IsPlaying)
        {
            if (Input.GetKey(KeyCode.LeftArrow) && modifier == EKeyboardModifier.Ctrl)
            {
                songAudioPlayer.PositionInSongInMillis -= 1;
            }
            if (Input.GetKey(KeyCode.RightArrow) && modifier == EKeyboardModifier.Ctrl)
            {
                songAudioPlayer.PositionInSongInMillis += 1;
            }
        }

        // Move and stretch notes
        UpdateInputToMoveAndStretchNotes(modifier);

        // Scroll and zoom in NoteArea
        UpdateInputToScrollAndZoom(modifier);
    }

    private void ToggleAudioPlayPause()
    {
        if (songAudioPlayer.IsPlaying)
        {
            songAudioPlayer.PauseAudio();
        }
        else
        {
            songAudioPlayer.PlayAudio();
        }
    }

    private void UpdateInputToScrollAndZoom(EKeyboardModifier modifier)
    {
        // Scroll with arroy keys
        if (Input.GetKeyUp(KeyCode.LeftArrow) && modifier == EKeyboardModifier.None)
        {
            noteArea.ScrollHorizontal(-1);
        }
        if (Input.GetKeyUp(KeyCode.RightArrow) && modifier == EKeyboardModifier.None)
        {
            noteArea.ScrollHorizontal(1);
        }

        // Zoom horizontal with Ctrl+'+' and Ctrl+'-'
        // Note: On my keyboard, the plus button has KeyCode.Equals but I don't know why.
        bool isPlusKeyUp = Input.GetKeyUp(KeyCode.Plus) || Input.GetKeyUp(KeyCode.KeypadPlus) || Input.GetKeyUp(KeyCode.Equals);
        bool isMinusKeyUp = Input.GetKeyUp(KeyCode.Minus) || Input.GetKeyUp(KeyCode.KeypadMinus);
        if (isPlusKeyUp && modifier == EKeyboardModifier.Ctrl)
        {
            noteArea.ZoomHorizontal(1);
        }
        if (isMinusKeyUp && modifier == EKeyboardModifier.Ctrl)
        {
            noteArea.ZoomHorizontal(-1);
        }

        // Zoom vertical with Ctrl+Shift+'+' and Ctrl+Shift+'-'
        if (isPlusKeyUp && modifier == EKeyboardModifier.CtrlShift)
        {
            noteArea.ZoomVertical(1);
        }
        if (isMinusKeyUp && modifier == EKeyboardModifier.CtrlShift)
        {
            noteArea.ZoomVertical(-1);
        }

        // Zoom and scroll with mouse wheel
        int scrollDirection = Math.Sign(Input.mouseScrollDelta.y);
        if (scrollDirection != 0 && noteArea.IsPointerOver)
        {
            // Scroll horizontal in NoteArea with mouse wheel
            if (modifier == EKeyboardModifier.None)
            {
                noteArea.ScrollHorizontal(scrollDirection);
            }

            // Zoom horizontal in NoteArea with Ctrl + mouse wheel
            if (modifier == EKeyboardModifier.Ctrl)
            {
                noteArea.ZoomHorizontal(scrollDirection);
            }

            // Scroll vertical in NoteArea with Shift + mouse wheel
            if (modifier == EKeyboardModifier.Shift)
            {
                noteArea.ScrollVertical(scrollDirection);
            }

            // Zoom vertical in NoteArea with Ctrl + Shift + mouse wheel
            if (modifier == EKeyboardModifier.CtrlShift)
            {
                noteArea.ZoomVertical(scrollDirection);
            }
        }
    }

    private void UpdateInputToMoveAndStretchNotes(EKeyboardModifier modifier)
    {
        Vector2 arrowKeyDirection = GetArrowKeyDirection();
        if (arrowKeyDirection == Vector2.zero)
        {
            return;
        }

        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        if (selectedNotes.IsNullOrEmpty())
        {
            return;
        }

        if (modifier == EKeyboardModifier.None)
        {
            return;
        }

        foreach (Note note in selectedNotes)
        {
            // Move with Shift
            if (modifier == EKeyboardModifier.Shift)
            {
                note.MoveHorizontal((int)arrowKeyDirection.x);
                note.MoveVertical((int)arrowKeyDirection.y);
            }

            // Move notes one octave up / down via Ctrl+Shift
            if (modifier == EKeyboardModifier.CtrlShift)
            {
                note.MoveVertical((int)arrowKeyDirection.y * 12);
            }

            // Extend right side with Alt
            if (modifier == EKeyboardModifier.Alt)
            {
                int newEndBeat = note.EndBeat + (int)arrowKeyDirection.x;
                if (newEndBeat > note.StartBeat)
                {
                    note.SetEndBeat(newEndBeat);
                }
            }

            // Extend left side with Ctrl
            if (modifier == EKeyboardModifier.Ctrl)
            {
                int newStartBeat = note.StartBeat + (int)arrowKeyDirection.x;
                if (newStartBeat < note.EndBeat)
                {
                    note.SetStartBeat(newStartBeat);
                }
            }

            // Adjust following notes.
            if (settings.SongEditorSettings.AdjustFollowingNotes)
            {
                AdjustFollowingNotes(modifier, arrowKeyDirection, selectedNotes);
            }
        }
        editorNoteDisplayer.UpdateNotesAndSentences();
    }

    private void AdjustFollowingNotes(EKeyboardModifier modifier, Vector2 arrowKeyDirection, List<Note> selectedNotes)
    {
        // Moving is applied to following notes as well.
        // When extending / shrinking the right side, then the following notes are move to compensate.
        List<Note> followingNotes = SongMetaUtils.GetFollowingNotes(songMeta, selectedNotes);
        foreach (Note note in followingNotes)
        {
            // Moved with Shift. The following notes are moved as well.
            if (modifier == EKeyboardModifier.Shift)
            {
                note.MoveHorizontal((int)arrowKeyDirection.x);
                note.MoveVertical((int)arrowKeyDirection.y);
            }

            // Extended right side with Alt. The following notes must be moved to compensate.
            if (modifier == EKeyboardModifier.Alt)
            {
                note.MoveHorizontal((int)arrowKeyDirection.x);
            }
        }
    }

    private Vector2 GetArrowKeyDirection()
    {
        Vector2 result = Vector2.zero;
        if (Input.GetKeyUp(KeyCode.LeftArrow))
        {
            result += new Vector2(-1, 0);
        }
        if (Input.GetKeyUp(KeyCode.RightArrow))
        {
            result += new Vector2(1, 0);
        }
        if (Input.GetKeyUp(KeyCode.UpArrow))
        {
            result += new Vector2(0, 1);
        }
        if (Input.GetKeyUp(KeyCode.DownArrow))
        {
            result += new Vector2(0, -1);
        }
        return result;
    }
}
