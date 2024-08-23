{#/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */#}

{%- let cbi = ci.get_callback_interface_definition(name).unwrap() %}
{%- let type_name = cbi|type_name(ci) %}
{%- let foreign_callback = format!("ForeignCallback{}", canonical_type_name) %}

{%- let vtable = cbi.vtable() %}
{%- let vtable_methods = cbi.vtable_methods() %}

{% if self.include_once_check("CallbackInterfaceRuntime.cs") %}{% include "CallbackInterfaceRuntime.cs" %}{% endif %}

{%- call cs::docstring(cbi, 0) %}
{{ config.access_modifier() }} interface {{ type_name }} {
    {%- for meth in cbi.methods() %}
    {%- call cs::docstring(meth, 4) %}
    {%- call cs::method_throws_annotation(meth.throws_type()) %}
    {%- match meth.return_type() %}
    {%- when Some with (return_type) %}
    {{ return_type|type_name(ci) }} {{ meth.name()|fn_name }}({% call cs::arg_list_decl(meth) %});
    {%- else %}
    void {{ meth.name()|fn_name }}({% call cs::arg_list_decl(meth) %});
    {%- endmatch %}
    {%- endfor %}
}

// The ForeignCallback that is passed to Rust.
class {{ foreign_callback }} {
    {%- for (ffi_callback, meth) in vtable_methods.iter() %}
    static {% call cs::ffi_return_type(ffi_callback) %} {{ meth.name()|var_name }}({% call cs::arg_list_ffi_decl_xx(ffi_callback) %}) {
        Console.WriteLine("XAXAXAXA");
        var uniffiObj = {{ type_|lift_fn }}(uniffiHandle);
        uniffiObj.{{ meth.name()|fn_name() }}(
            {%- for arg in meth.arguments() -%}
            {{ arg|lift_fn }}({{ arg.name()|var_name }})
            {%- if !loop.last %}, {% endif -%}
            {%- endfor -%}
        );
    }
    {% endfor %}

    static void uniffiFree(ulong handle) {
        {{ ffi_converter_name }}.INSTANCE.Drop(handle);
    }

    // vtable struct must be static. Storing references to static methods in a static struct ensures
    // that implicit delegates are not garbage collected (referencing a static method creates an
    // implicit delegate object).
    // public static GCHandle _uniffiVTable = GCHandle.Alloc(new _UniFFILib.{{ vtable|ffi_type_name }} {
    //     {%- for (ffi_callback, meth) in vtable_methods.iter() %}
    //     // {{ meth.name()|var_name() }} = GCHandle.Alloc({{ meth.name()|var_name() }}, GCHandleType.Pinned).AddrOfPinnedObject(),
    //     {{ meth.name()|var_name() }} = {{ meth.name()|var_name() }},
    //     {%- endfor %}
    //     uniffiFree = uniffiFree
    //     // uniffiFree = GCHandle.Alloc(uniffiFree, GCHandleType.Pinned).AddrOfPinnedObject()
    // }, GCHandleType.Pinned);


    // ############################################################################################
    // BIG TODO: I THINK CASTING STATIC FUNCTION TO A DELEGATE IS WRONG. REFERNCING STATIC FUNCTION
    // SIMPLY CREATES A NEW, LOCAL DELEGATE OBJECT, ALLOWING GC TO COLLECT IT.
    // ############################################################################################

    public static _UniFFILib.{{ vtable|ffi_type_name }} _uniffiVTable = new _UniFFILib.{{ vtable|ffi_type_name }} {
        {%- for (ffi_callback, meth) in vtable_methods.iter() %}
        // {{ meth.name()|var_name() }} = GCHandle.Alloc({{ meth.name()|var_name() }}, GCHandleType.Pinned).AddrOfPinnedObject(),
        {{ meth.name()|var_name() }} = Marshal.GetFunctionPointerForDelegate((_UniFFILib.{{ ffi_callback.name()|ffi_callback_name }}){{ meth.name()|var_name() }}),
        {%- endfor %}
        @uniffiFree = Marshal.GetFunctionPointerForDelegate((_UniFFILib.UniffiCallbackInterfaceFree)@uniffiFree)
        // uniffiFree = GCHandle.Alloc(uniffiFree, GCHandleType.Pinned).AddrOfPinnedObject()
    };

    {#
    // public static ForeignCallback INSTANCE = (ulong handle, uint method, IntPtr argsData, int argsLength, ref RustBuffer outBuf) => {
    //     var cb = {{ type_|lift_fn }}(handle);
    //     switch (method) {
    //         case 0: {
    //             // 0 means Rust is done with the callback, and the callback
    //             // can be dropped by the foreign language.
    //             {{ ffi_converter_name }}.INSTANCE.Drop(handle);
    //             // No return value.
    //             // See docs of ForeignCallback in `uniffi/src/ffi/foreigncallbacks.rs`
    //             return 0;
    //         }

    //         {% for meth in cbi.methods() -%}
    //         {%- let method_name = meth.name()|fn_name %}
    //         {%- let method_name = format!("Invoke{}", method_name) %}
    //         case {{ loop.index }}: {
    //             try {
    //                 {%- match meth.throws_type() %}
    //                 {%- when Some(error_type) %}
    //                 try {
    //                     outBuf = {{ method_name }}(cb, RustBuffer.MemoryStream(argsData, argsLength));
    //                     return UniffiCallbackResponseCode.SUCCESS;
    //                 } catch ({{ error_type|type_name(ci) }} e) {
    //                     outBuf = {{ error_type|lower_fn }}(e);
    //                     return UniffiCallbackResponseCode.ERROR;
    //                 }
    //                 {%- else %}
    //                 outBuf = {{ method_name }}(cb, RustBuffer.MemoryStream(argsData, argsLength));
    //                 return UniffiCallbackResponseCode.SUCCESS;
    //                 {%- endmatch %}
    //             } catch (Exception e) {
    //                 // Unexpected error
    //                 try {
    //                     // Try to serialize the error into a string
    //                     outBuf = {{ Type::String.borrow()|lower_fn }}(e.Message);
    //                 } catch {
    //                     // If that fails, then it's time to give up and just return
    //                 }
    //                 return UniffiCallbackResponseCode.UNEXPECTED_ERROR;
    //             }
    //         }

    //         {% endfor %}
    //         default: {
    //             // This should never happen, because an out of bounds method index won't
    //             // ever be used. Once we can catch errors, we should return an InternalException.
    //             // https://github.com/mozilla/uniffi-rs/issues/351
    //             return UniffiCallbackResponseCode.UNEXPECTED_ERROR;
    //         }
    //     }
    // };

    // {% for meth in cbi.methods() -%}
    // {% let method_name = meth.name()|fn_name -%}
    // {% let method_name = format!("Invoke{}", method_name) -%}
    // static RustBuffer {{ method_name }}({{ type_name }} callback, BigEndianStream stream) {
    //     {%- match meth.return_type() %}
    //     {%- when Some with (return_type) %}
    //     var result =
    //     {%- when None -%}
    //     {%- endmatch -%}
    //     callback.{{ meth.name()|fn_name }}(
    //             {%- for arg in meth.arguments() -%}
    //             {{ arg|read_fn }}(stream)
    //             {%- if !loop.last %}, {% endif -%}
    //             {%- endfor -%}
    //             );

    //     {%- match meth.return_type() %}
    //     {%- when Some with (return_type) %}
    //     return {{ return_type|ffi_converter_name }}.INSTANCE.LowerIntoRustBuffer(result);
    //     {%- else %}
    //     return new RustBuffer();
    //     {%- endmatch %}
    // }

    // {% endfor %}
    #}
}

// The ffiConverter which transforms the Callbacks in to Handles to pass to Rust.
class {{ ffi_converter_name }}: FfiConverterCallbackInterface<{{ type_name }}> {
    public static {{ ffi_converter_name }} INSTANCE = new {{ ffi_converter_name }}();

    static RandomStruct RANDOM_STRUCT = new RandomStruct{a=100, b=101, c=103, d=104};

    public override void Register() {
        Console.WriteLine("Register");


        // _UniFFILib.{{ cbi.ffi_init_callback().name() }}({{ foreign_callback }}._uniffiVTable.AddrOfPinnedObject());
        // ref *(SomeStruct*)ptr.ToPointer()
        // unsafe {
        //     _UniFFILib.{{ cbi.ffi_init_callback().name() }}(
        //         ref *(_UniFFILib.{{ vtable|ffi_type_name }}*){{ foreign_callback }}._uniffiVTable.AddrOfPinnedObject());
        // }
        // _UniFFILib.{{ cbi.ffi_init_callback().name() }}(
        //     ref Marshal.PtrToStructure<_UniFFILib.{{ vtable|ffi_type_name }}>({{ foreign_callback }}._uniffiVTable.AddrOfPinnedObject()));
        // _UniFFILib.{{ cbi.ffi_init_callback().name() }}(ref Marshal.PtrToStructure<_UniFFILib.{{ vtable|ffi_type_name }}>({{ foreign_callback }}._uniffiVTable.AddrOfPinnedObject()));
        _UniFFILib.{{ cbi.ffi_init_callback().name() }}(ref {{ foreign_callback }}._uniffiVTable);

        // var handle = GCHandle.Alloc(new RandomStruct{a=100, b=101, c=103, d=104}, GCHandleType.Pinned);
        // _UniFFILib.{{ cbi.ffi_init_callback().name() }}(handle.AddrOfPinnedObject());
        // _UniFFILib.{{ cbi.ffi_init_callback().name() }}(ref RANDOM_STRUCT);
    }
}
