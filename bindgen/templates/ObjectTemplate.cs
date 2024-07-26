{#/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */#}

{%- let obj = ci.get_object_definition(name).unwrap() %}
{%- if self.include_once_check("ObjectRuntime.cs") %}{% include "ObjectRuntime.cs" %}{% endif %}

{%- call cs::docstring(obj, 0) %}
{{ config.access_modifier() }} interface I{{ type_name }} {
    {% for meth in obj.methods() -%}
    {%- call cs::docstring(meth, 4) %}
    {%- call cs::method_throws_annotation(meth.throws_type()) %}
    {% match meth.return_type() -%} {%- when Some with (return_type) -%} {{ return_type|type_name(ci) }} {%- when None %}void{%- endmatch %} {{ meth.name()|fn_name }}({% call cs::arg_list_decl(meth) %});
    {% endfor %}
}

{%- call cs::docstring(obj, 0) %}
{{ config.access_modifier() }} class {{ type_name }}: I{{ type_name }} {
    IntPtr _uniffiPointer;

    public {{ type_name }}(IntPtr pointer)
    {
        _uniffiPointer = pointer;
    }

    {%- match obj.primary_constructor() %}
    {%- when Some with (cons) %}
    {%- call cs::docstring(cons, 4) %}
    public {{ type_name }}({% call cs::arg_list_decl(cons) -%}) :
        this({% call cs::to_ffi_call(cons) %}) {}
    {%- when None %}
    {%- endmatch %}

    // Finalizer, release Rust object reference
    ~{{ type_name }}() {
        _UniffiHelpers.RustCall((ref UniffiRustCallStatus status) => {
            _UniFFILib.{{ obj.ffi_object_free().name() }}(this._uniffiPointer, ref status);
        });
    }

    {% for meth in obj.methods() -%}
    {%- call cs::docstring(meth, 4) %}
    {%- call cs::method_throws_annotation(meth.throws_type()) %}
    {%- match meth.return_type() -%}

    {%- when Some with (return_type) %}
    public {{ return_type|type_name(ci) }} {{ meth.name()|fn_name }}({% call cs::arg_list_decl(meth) %}) {
        return {{ return_type|lift_fn }}({%- call cs::to_ffi_call_with_prefix("this._uniffiClonePointer()", meth) %});
    }

    {%- when None %}
    public void {{ meth.name()|fn_name }}({% call cs::arg_list_decl(meth) %}) {
        {%- call cs::to_ffi_call_with_prefix("this._uniffiClonePointer()", meth) %};
    }
    {% endmatch %}
    {% endfor %}

    {%- for tm in obj.uniffi_traits() -%}
    {%- match tm %}
    {%- when UniffiTrait::Display { fmt } %}
    public override string ToString() {
        return {{ Type::String.borrow()|lift_fn }}({%- call cs::to_ffi_call_with_prefix("this._uniffiClonePointer()", fmt) %});
    }
    {%- else %}
    {%- endmatch %}
    {%- endfor %}

    {% if !obj.alternate_constructors().is_empty() -%}
    {% for cons in obj.alternate_constructors() -%}
    {%- call cs::docstring(cons, 4) %}
    {%- call cs::method_throws_annotation(cons.throws_type()) %}
    public static {{ type_name }} {{ cons.name()|fn_name }}({% call cs::arg_list_decl(cons) %}) {
        return new {{ type_name }}({% call cs::to_ffi_call(cons) %});
    }
    {% endfor %}
    {% endif %}

    public IntPtr _uniffiClonePointer() {
        return _UniffiHelpers.RustCall((ref UniffiRustCallStatus status) => {
            return _UniFFILib.{{ obj.ffi_object_clone().name() }}(this._uniffiPointer, ref status);
        });
    }
}

class {{ obj|ffi_converter_name }}: FfiConverter<{{ type_name }}, IntPtr> {
    public static {{ obj|ffi_converter_name }} INSTANCE = new {{ obj|ffi_converter_name }}();

    public override IntPtr Lower({{ type_name }} value) {
        return value._uniffiClonePointer();
    }

    public override {{ type_name }} Lift(IntPtr value) {
        return new {{ type_name }}(value);
    }

    public override {{ type_name }} Read(BigEndianStream stream) {
        return Lift(new IntPtr(stream.ReadLong()));
    }

    public override int AllocationSize({{ type_name }} value) {
        return 8;
    }

    public override void Write({{ type_name }} value, BigEndianStream stream) {
        stream.WriteLong(Lower(value).ToInt64());
    }
}
