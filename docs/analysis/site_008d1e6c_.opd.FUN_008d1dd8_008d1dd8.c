// hook SITE 0x008d1e6c

longlong _opd_FUN_008d1dd8(undefined4 param_1,undefined4 param_2,undefined4 param_3,
                          undefined4 param_4,undefined4 param_5,int param_6,int param_7)

{
  undefined4 *puVar1;
  undefined *puVar2;
  int iVar3;
  undefined4 uVar4;
  int iVar5;
  undefined4 *puVar6;
  undefined4 *puVar7;
  longlong lVar8;
  undefined8 uVar9;
  longlong lVar10;
  
  puVar2 = PTR_PTR_0121b948;
  uVar9 = *(undefined8 *)(PTR_PTR_0121b948 + -0x7f10);
  iVar3 = _opd_FUN_000be778(0xaa0,uVar9);
  lVar8 = 0;
  if (iVar3 != 0) {
    lVar8 = _opd_FUN_000bead8(iVar3,uVar9,*(undefined4 *)(puVar2 + -0x7f08),
                              *(undefined4 *)(puVar2 + -0x7f04));
    if (lVar8 == 0) {
      _opd_FUN_000c1e00(iVar3);
    }
    else {
      lVar10 = 10;
      *(undefined4 *)(iVar3 + 0xa84) = 0x96;
      puVar6 = (undefined4 *)(iVar3 + 0x78);
      iVar5 = 0;
      *(undefined4 *)(iVar3 + 0x60) = param_1;
      *(undefined4 *)(iVar3 + 100) = param_2;
      *(undefined4 *)(iVar3 + 0x68) = param_3;
      *(undefined4 *)(iVar3 + 0x6c) = param_4;
      *(undefined4 *)(iVar3 + 0x70) = param_5;
      *(undefined1 *)(iVar3 + 0xa8c) = 0;
      *(undefined4 *)(iVar3 + 0xa80) = 0x80;
      *(undefined4 *)(iVar3 + 0xa70) = 0;
      *(undefined4 *)(iVar3 + 0xa74) = 0;
      *(undefined4 *)(iVar3 + 0xa90) = 0;
      *(undefined4 *)(iVar3 + 0xa94) = 0;
      *(undefined4 *)(iVar3 + 0xa98) = 0;
      *(undefined4 *)(iVar3 + 0xa78) = 0;
      *(undefined4 *)(iVar3 + 0xa7c) = 0;
      *(undefined4 *)(iVar3 + 0xa88) = 0;
      while( true ) {
        puVar1 = (undefined4 *)(iVar5 + param_6);
        puVar7 = (undefined4 *)(iVar5 + param_7);
        iVar5 = iVar5 + 4;
        *puVar6 = *puVar1;
        uVar4 = *puVar7;
        puVar6[0x35] = 0;
        puVar6[1] = uVar4;
        *(undefined8 *)(puVar6 + 4) = 0;
        *(undefined8 *)(puVar6 + 0x28) = 0;
        puVar6[6] = 0;
        puVar6[2] = 0;
        puVar6[0x37] = 1;
        puVar6[0x38] = 0;
        puVar6[0x36] = 0;
        puVar6[0x2a] = 0;
        puVar6[0x30] = 0;
        puVar6[0x2b] = 0;
        puVar6[0x31] = 0;
        puVar6[0x2c] = 0;
        puVar6[0x32] = 0;
        puVar6[0x2d] = 0;
        puVar6[0x33] = 0;
        puVar6[0x2e] = 0;
        puVar6[0x34] = 0;
        puVar6[0x2f] = 0;
        lVar10 = lVar10 + -1;
        if (lVar10 == 0) break;
        puVar6 = puVar6 + 0x3a;
      }
      uVar4 = *(undefined4 *)(puVar2 + -0x7efc);
      *(undefined4 *)(iVar3 + 0xa94) = *(undefined4 *)(puVar2 + -0x7f00);
      *(undefined4 *)(iVar3 + 0xa98) = uVar4;
      if (*(uint *)(iVar3 + 100) < 10) {
                    /* WARNING: Could not recover jumptable at 0x008d205c. Too many branches */
                    /* WARNING: Treating indirect jump as call */
        lVar8 = (*(code *)(*(int *)(*(uint *)(iVar3 + 100) * 4 + *(int *)(puVar2 + -0x7ef8)) +
                          *(int *)(puVar2 + -0x7ef8)))();
        return lVar8;
      }
      *(undefined4 *)(iVar3 + 0x9a0) = 0;
      *(undefined4 *)(iVar3 + 0x98c) = 0;
      *(undefined4 *)(iVar3 + 0x988) = 0;
      *(undefined8 *)(iVar3 + 0x998) = 0;
      *(undefined8 *)(iVar3 + 0xa28) = 0;
      if (*(int *)(iVar3 + 0x68) == 0) {
        uVar4 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7eb0));
        *(undefined4 *)(iVar3 + 0x990) = uVar4;
      }
      else {
        uVar4 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7eac));
        *(undefined4 *)(iVar3 + 0x990) = uVar4;
      }
      uVar4 = *(undefined4 *)(puVar2 + -0x7ea8);
      *(undefined4 *)(iVar3 + 0xa60) = 0;
      *(undefined4 *)(iVar3 + 0xa64) = 1;
      *(undefined4 *)(iVar3 + 0x34) = uVar4;
      _opd_FUN_008d0698(iVar3);
    }
  }
  return lVar8;
}

