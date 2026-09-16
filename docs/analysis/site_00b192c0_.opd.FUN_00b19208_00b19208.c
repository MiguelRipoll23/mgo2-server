// hook SITE 0x00b192c0

void _opd_FUN_00b19208(undefined4 param_1,undefined4 param_2,undefined4 param_3)

{
  undefined *puVar1;
  undefined4 uVar2;
  undefined *puVar3;
  undefined1 auStack_c0 [136];
  
  puVar1 = PTR_PTR_0121bd14;
  uVar2 = _opd_FUN_00036350();
  puVar3 = (undefined *)_opd_FUN_000cdc90(uVar2);
  _opd_FUN_000d60b8(auStack_c0,0x80);
  if (puVar3 == &UNK_00f8c767) {
    uVar2 = *(undefined4 *)(puVar1 + -0x7ed8);
  }
  else if ((int)puVar3 < 0xf8c768) {
    if (puVar3 == &UNK_00f8c6e7) {
      uVar2 = *(undefined4 *)(puVar1 + -0x7ee8);
    }
    else if ((int)puVar3 < 0xf8c6e8) {
      if (puVar3 == &UNK_00f8c6a7) {
        uVar2 = *(undefined4 *)(puVar1 + -0x7ef0);
      }
      else if (puVar3 == &UNK_00f8c6c7) {
        uVar2 = *(undefined4 *)(puVar1 + -0x7eec);
      }
      else {
        if (puVar3 != &UNK_00f8c687) goto LAB_00b192c0;
        uVar2 = *(undefined4 *)(puVar1 + -0x7ef4);
      }
    }
    else if (puVar3 == &UNK_00f8c727) {
      uVar2 = *(undefined4 *)(puVar1 + -0x7ee0);
    }
    else if (puVar3 == &UNK_00f8c747) {
      uVar2 = *(undefined4 *)(puVar1 + -0x7edc);
    }
    else {
      if (puVar3 != &UNK_00f8c707) goto LAB_00b192c0;
      uVar2 = *(undefined4 *)(puVar1 + -0x7ee4);
    }
  }
  else if (puVar3 == &UNK_00f8caa7) {
    uVar2 = *(undefined4 *)(puVar1 + -0x7ec8);
  }
  else if ((int)puVar3 < 0xf8caa8) {
    if (puVar3 == &UNK_00f8ca67) {
      uVar2 = *(undefined4 *)(puVar1 + -0x7ed0);
    }
    else {
      if (puVar3 == &UNK_00f8ca87) {
LAB_00b193ec:
        uVar2 = _opd_FUN_000cdc90(*(undefined4 *)(puVar1 + -0x7ecc));
        uVar2 = _opd_FUN_00b30bb0(uVar2);
        _opd_FUN_0023c2b0(auStack_c0,0x80,uVar2);
        goto LAB_00b1937c;
      }
      if (puVar3 != &UNK_00f8c787) goto LAB_00b192c0;
      uVar2 = *(undefined4 *)(puVar1 + -0x7ed4);
    }
  }
  else if (puVar3 == &UNK_00f8cae7) {
    uVar2 = *(undefined4 *)(puVar1 + -0x7ec0);
  }
  else if ((int)puVar3 < 0xf8cae8) {
    if (puVar3 != &UNK_00f8cac7) {
LAB_00b192c0:
      uVar2 = _opd_FUN_00036350();
      _opd_FUN_00b310a0(param_1,param_2,uVar2,param_3);
      return;
    }
    uVar2 = *(undefined4 *)(puVar1 + -0x7ec4);
  }
  else {
    if (puVar3 != (undefined *)0xf8cb07) {
      if (puVar3 != (undefined *)0xf8cb67) goto LAB_00b192c0;
      goto LAB_00b193ec;
    }
    uVar2 = *(undefined4 *)(puVar1 + -0x7ebc);
  }
  uVar2 = _opd_FUN_000cdc90(uVar2);
  uVar2 = _opd_FUN_00b30bb0(uVar2);
  _opd_FUN_0023c2b0(auStack_c0,0x80,uVar2);
LAB_00b1937c:
  _opd_FUN_00b310a0(param_1,param_2,auStack_c0,0);
  return;
}

