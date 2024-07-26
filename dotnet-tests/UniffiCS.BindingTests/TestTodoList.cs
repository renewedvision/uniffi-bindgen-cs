// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using uniffi.todolist;

namespace UniffiCS.BindingTests;

public class TestTodoList
{
    [Fact]
    public void TodoListWorks()
    {
        var todo = new TodoList();

        Assert.Throws<TodoException.EmptyTodoList>(() => todo.GetLast());

        Assert.Throws<TodoException.EmptyString>(() => TodolistMethods.CreateEntryWith(""));

        todo.AddItem("Write strings support");
        Assert.Equal("Write strings support", todo.GetLast());

        todo.AddItem("Write tests for strings support");
        Assert.Equal("Write tests for strings support", todo.GetLast());

        var entry = TodolistMethods.CreateEntryWith("Write bindings for strings as record members");
        todo.AddEntry(entry);
        Assert.Equal("Write bindings for strings as record members", todo.GetLast());
        Assert.Equal("Write bindings for strings as record members", todo.GetLastEntry().text);

        todo.AddItem("Test Ünicode hàndling without an entry 🤣");
        Assert.Equal("Test Ünicode hàndling without an entry 🤣", todo.GetLast());

        var entry2 = new TodoEntry("Test Ünicode hàndling in an entry 🤣");
        todo.AddEntry(entry2);
        Assert.Equal("Test Ünicode hàndling in an entry 🤣", todo.GetLastEntry().text);

        Assert.Equal(5, todo.GetEntries().Count);

        todo.AddEntries(new List<TodoEntry>() { new TodoEntry("foo"), new TodoEntry("bar") });
        Assert.Equal(7, todo.GetEntries().Count);
        Assert.Equal("bar", todo.GetLastEntry().text);

        todo.AddItems(new List<string>() { "bobo", "fofo" });
        Assert.Equal(9, todo.GetItems().Count);
        Assert.Equal("bobo", todo.GetItems()[7]);

        Assert.Null(TodolistMethods.GetDefaultList());

        // https://github.com/xunit/xunit/issues/2027
#pragma warning disable CS8602

        // Note that each individual object instance needs to be explicitly destroyed,
        // either by using the `.use` helper or explicitly calling its `.destroy` method.
        // Failure to do so will leak the underlying Rust object.
        var todo2 = new TodoList();
        TodolistMethods.SetDefaultList(todo);

        Assert.Equal(todo.GetEntries(), TodolistMethods.GetDefaultList().GetEntries());
        Assert.NotEqual(todo2.GetEntries(), TodolistMethods.GetDefaultList().GetEntries());

        todo2.MakeDefault();
        Assert.NotEqual(todo.GetEntries(), TodolistMethods.GetDefaultList().GetEntries());
        Assert.Equal(todo2.GetEntries(), TodolistMethods.GetDefaultList().GetEntries());

        todo.AddItem("Test liveness after being demoted from default");
        Assert.Equal("Test liveness after being demoted from default", todo.GetLast());

        todo2.AddItem("Test shared state through local vs default reference");
        Assert.Equal("Test shared state through local vs default reference", TodolistMethods.GetDefaultList().GetLast());

#pragma warning restore CS8602
    }
}
