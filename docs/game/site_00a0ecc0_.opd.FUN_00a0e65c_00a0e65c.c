// hook SITE 0x00a0ecc0

undefined4 _opd_FUN_00a0e65c(int param_1,int param_2)

{
  undefined *puVar1;
  uint *puVar2;
  undefined4 uVar3;
  uint *puVar4;
  uint *puVar5;
  uint *puVar6;
  uint *puVar7;
  int iVar8;
  int iVar9;
  int iVar10;
  int iVar11;
  char cVar14;
  int *piVar12;
  undefined4 uVar13;
  undefined8 uVar15;
  undefined1 auStack_f0 [32];
  undefined1 auStack_d0 [72];
  
  puVar1 = PTR_PTR_0121bb48;
  puVar2 = (uint *)_opd_FUN_00242bf0(param_1,0x1dc487);
  if (puVar2 != (uint *)0x0) {
    uVar3 = _opd_FUN_0023ebd8(0xf914bf,3);
    _opd_FUN_00245968(param_1,puVar2,uVar3);
    if ((param_2 == 0) && ((*puVar2 >> 0x16 & 1) != 0)) {
      *puVar2 = *puVar2 & 0xffbfffff | 0x40000000;
    }
  }
  puVar4 = (uint *)_opd_FUN_00242bf0(param_1,0x47180e);
  if ((puVar4 != (uint *)0x0) && ((*puVar4 >> 0x16 & 1) != 0)) {
    *puVar4 = *puVar4 & 0xffbfffff | 0x40000000;
  }
  puVar5 = (uint *)_opd_FUN_00242bf0(param_1,0xccf724);
  if ((puVar5 != (uint *)0x0) && ((*puVar5 >> 0x16 & 1) == 0)) {
    *puVar5 = *puVar5 | 0x40400000;
  }
  puVar5 = (uint *)_opd_FUN_00242bf0(param_1,0x15be1b);
  if ((puVar5 != (uint *)0x0) && ((*puVar5 >> 0x16 & 1) != 0)) {
    *puVar5 = *puVar5 & 0xffbfffff | 0x40000000;
  }
  puVar6 = (uint *)_opd_FUN_00242bf0(param_1,&DAT_00fd10aa);
  if ((puVar6 != (uint *)0x0) && ((*puVar6 >> 0x16 & 1) != 0)) {
    *puVar6 = *puVar6 & 0xffbfffff | 0x40000000;
  }
  puVar7 = (uint *)_opd_FUN_00242bf0(param_1,0x77a11b);
  if ((puVar7 != (uint *)0x0) && ((*puVar7 >> 0x16 & 1) != 0)) {
    *puVar7 = *puVar7 & 0xffbfffff | 0x40000000;
  }
  iVar8 = _opd_FUN_00280418();
  if (iVar8 == 0) {
    return 0xffffffff;
  }
  iVar9 = _opd_FUN_00f045d0(iVar8);
  if (iVar9 == 0) goto LAB_00a0ebfc;
  iVar9 = _opd_FUN_00f06488(iVar8);
  if (iVar9 == 0) {
    return 0xffffffff;
  }
  if (puVar2 != (uint *)0x0) {
    if ((*puVar2 >> 0x16 & 1) == 0) {
      *puVar2 = *puVar2 | 0x40400000;
    }
    _opd_FUN_0023be50(auStack_f0,0x20,iVar9 + 4);
    _opd_FUN_00245968(param_1,puVar2,auStack_f0);
  }
  if ((puVar4 != (uint *)0x0) && (param_2 == 0)) {
    iVar8 = _opd_FUN_00f04688(iVar8);
    if ((*puVar4 >> 0x16 & 1) == 0) {
      *puVar4 = *puVar4 | 0x40400000;
    }
    _opd_FUN_0023be50(auStack_f0,0x20,iVar8 + 0x18);
    _opd_FUN_00245968(param_1,puVar4,auStack_f0);
  }
  if ((*(int *)(iVar9 + 0x2300) == 0) || (1 < (byte)(*(char *)(iVar9 + 0x2315) - 1U)))
  goto LAB_00a0ebfc;
  if ((puVar6 != (uint *)0x0) && (puVar5 != (uint *)0x0)) {
    if ((*puVar6 >> 0x16 & 1) == 0) {
      *puVar6 = *puVar6 | 0x40400000;
    }
    _opd_FUN_0023be50(auStack_f0,0x20,iVar9 + 0x2304);
    _opd_FUN_00245968(param_1,puVar6,auStack_f0);
    if (*(char *)(iVar9 + 0x2315) == '\x02') {
      if ((*puVar5 >> 0x16 & 1) == 0) {
        *puVar5 = *puVar5 | 0x40400000;
      }
      uVar15 = 0x1c;
    }
    else {
      if (*(char *)(iVar9 + 0x2315) != '\x01') goto LAB_00a0ea60;
      if ((*puVar5 >> 0x16 & 1) == 0) {
        *puVar5 = *puVar5 | 0x40400000;
      }
      uVar15 = 0x21;
    }
    uVar3 = _opd_FUN_0023ebd8(0xf914bf,uVar15);
    _opd_FUN_00245968(param_1,puVar5,uVar3);
  }
LAB_00a0ea60:
  if (((puVar7 == (uint *)0x0) || (iVar8 = _opd_FUN_0097c124(), iVar8 == 0)) ||
     (*(char *)(iVar8 + 0x290) != '\x03')) goto LAB_00a0ebfc;
  if ((*puVar7 >> 0x16 & 1) == 0) {
    *puVar7 = *puVar7 | 0x40400000;
  }
  iVar8 = _opd_FUN_0097c124();
  if (iVar8 == 0) {
LAB_00a0ead8:
    iVar8 = _opd_FUN_00255678(param_1,puVar7);
  }
  else {
    if (param_1 == *(int *)(iVar8 + 0x2a4)) {
      iVar8 = *(int *)(iVar8 + 0x28c);
    }
    else {
      iVar8 = _opd_FUN_00256a20(param_1,*(undefined4 *)(iVar8 + 0x288));
    }
    if (iVar8 < 0) goto LAB_00a0ead8;
  }
  if ((*puVar7 >> 0x16 & 1) != 0) {
    *puVar7 = *puVar7 & 0xffbfffff | 0x40000000;
  }
  if ((param_1 != 0) && (iVar10 = _opd_FUN_00280418(), iVar10 != 0)) {
    iVar11 = _opd_FUN_00f045d0(iVar10);
    if (((iVar11 != 0) &&
        ((iVar10 = _opd_FUN_00f06488(iVar10), iVar10 != 0 && (*(char *)(iVar9 + 0x2338) == '\x03')))
        ) && (iVar11 = _opd_FUN_0097c124(), iVar11 != 0)) {
      if (*(int *)(iVar10 + 0x2300) == *(int *)(iVar9 + 0x2300)) {
        uVar3 = *(undefined4 *)(iVar11 + 0x288);
      }
      else {
        uVar3 = *(undefined4 *)(iVar11 + 0x294);
      }
      cVar14 = _opd_FUN_00c610e8(uVar3,iVar9 + 0x2339);
      if ((((cVar14 != '\0') && (iVar9 = _opd_FUN_002555a0(param_1,iVar8,uVar3), iVar9 == 0)) &&
          (iVar8 = _opd_FUN_00255960(puVar7,iVar8), iVar8 == 0)) && ((*puVar7 >> 0x16 & 1) == 0)) {
        *puVar7 = *puVar7 | 0x40400000;
      }
    }
  }
LAB_00a0ebfc:
  iVar8 = _opd_FUN_00242bf0(param_1,0x56f1c1);
  if (iVar8 != 0) {
    _opd_FUN_000d60b8(auStack_d0,0x40);
    piVar12 = (int *)_opd_FUN_00f30388();
    uVar3 = (*(code *)**(undefined4 **)(*piVar12 + 0x18))(piVar12,*(undefined4 *)(puVar1 + -0x7fc4))
    ;
    uVar13 = _opd_FUN_0023ebd8(0xf914bf,0x50);
    uVar3 = _opd_FUN_00d734c0(uVar3,0,0);
    _opd_FUN_00fa1c08(auStack_d0,uVar13,uVar3);
    _opd_FUN_00ead038(param_1,iVar8,auStack_d0);
  }
  return 0;
}

