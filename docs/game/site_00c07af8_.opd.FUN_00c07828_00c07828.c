// hook SITE 0x00c07af8

longlong _opd_FUN_00c07828(void)

{
  uint uVar1;
  undefined *puVar2;
  int iVar3;
  undefined *puVar4;
  undefined4 uVar5;
  undefined4 uVar6;
  int *piVar7;
  int iVar8;
  longlong lVar9;
  undefined8 uVar10;
  undefined1 auStack_90 [32];
  
  puVar2 = PTR_PTR_0121be10;
  uVar10 = *(undefined8 *)(PTR_PTR_0121be10 + -0x7ff8);
  lVar9 = 0;
  iVar3 = _opd_FUN_000be778(0xb0,uVar10);
  if (iVar3 != 0) {
    lVar9 = _opd_FUN_000bead8(iVar3,uVar10,*(undefined4 *)(puVar2 + -0x7ff0),
                              *(undefined4 *)(puVar2 + -0x7fec));
    if (lVar9 == 0) {
      _opd_FUN_000c1e00(iVar3);
    }
    else {
      *(undefined4 *)(iVar3 + 0x84) = 0;
      *(int *)(iVar3 + 0x68) = iVar3 + 0x6c;
      *(undefined4 *)(iVar3 + 0x80) = 9000;
      *(undefined4 *)(iVar3 + 0x88) = 0;
      *(undefined4 *)(iVar3 + 0x8c) = 0;
      *(undefined4 *)(iVar3 + 0x70) = 0;
      *(undefined4 *)(iVar3 + 0x74) = 0;
      *(undefined4 *)(iVar3 + 0x7c) = 9000;
LAB_00c07938:
      puVar4 = (undefined *)_opd_FUN_000d8450();
      if (puVar4 != (undefined *)0x0) {
        while( true ) {
          if (puVar4 == (undefined *)0x39d643) break;
          if (0x39d643 < (int)puVar4) {
            if (puVar4 == (undefined *)0x4899d8) {
              *(undefined4 *)(iVar3 + 0x74) = 1;
            }
            else if (puVar4 == &UNK_00983d71) {
              uVar5 = _opd_FUN_000db2f8();
              *(undefined4 *)(iVar3 + 0x8c) = uVar5;
            }
            else if (puVar4 == &UNK_003d0cae) {
              uVar5 = _opd_FUN_000db2f8();
              *(undefined4 *)(iVar3 + 0x90) = uVar5;
            }
            goto LAB_00c07938;
          }
          if (puVar4 == (undefined *)0x1bd06) {
            puVar4 = (undefined *)_opd_FUN_000db2f8();
            if (puVar4 == (undefined *)0xbfa95f) {
              *(undefined4 *)(iVar3 + 0x94) = 0;
            }
            else {
              *(undefined4 *)(iVar3 + 0x94) = 1;
              if (puVar4 == &UNK_00b16a36) {
                *(undefined4 *)(iVar3 + 0x98) = 1;
              }
            }
            uVar5 = _opd_FUN_00242400(puVar4,0,1,200);
            *(undefined4 *)(iVar3 + 0x60) = uVar5;
            goto LAB_00c07938;
          }
          if (puVar4 != (undefined *)0x468ed) goto LAB_00c07938;
          uVar5 = _opd_FUN_000db2f8();
          *(undefined4 *)(iVar3 + 0x78) = 1;
          *(undefined4 *)(iVar3 + 0x7c) = uVar5;
          *(undefined4 *)(iVar3 + 0x80) = uVar5;
          puVar4 = (undefined *)_opd_FUN_000d8450();
          if (puVar4 == (undefined *)0x0) goto LAB_00c07988;
        }
        uVar5 = _opd_FUN_000db2f8();
        *(undefined4 *)(iVar3 + 0x88) = uVar5;
        goto LAB_00c07938;
      }
LAB_00c07988:
      uVar5 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0xf17b70);
      *(undefined4 *)(iVar3 + 0xa4) = uVar5;
      uVar5 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0xedc996);
      *(undefined4 *)(iVar3 + 0xa8) = uVar5;
      uVar5 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0xedc997);
      *(undefined4 *)(iVar3 + 0xac) = uVar5;
      uVar5 = _opd_FUN_0023e4a0(0xf8a72d,0xe2bd61);
      uVar6 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0x19a408);
      _opd_FUN_00245968(*(undefined4 *)(iVar3 + 0x60),uVar6,uVar5);
      uVar6 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0xb2ab24);
      _opd_FUN_00245968(*(undefined4 *)(iVar3 + 0x60),uVar6,uVar5);
      if (*(int *)(iVar3 + 0x94) != 0) {
        uVar5 = _opd_FUN_0023e4a0(0xf8a72d,0x7e88a0);
        uVar6 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0x902637);
        _opd_FUN_00245968(*(undefined4 *)(iVar3 + 0x60),uVar6,uVar5);
        uVar6 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0xac989b);
        _opd_FUN_00245968(*(undefined4 *)(iVar3 + 0x60),uVar6,uVar5);
        uVar1 = (int)*(uint *)(iVar3 + 0x98) >> 0x1f;
        uVar5 = _opd_FUN_0023e4a0(0xf8a72d,((int)(((uVar1 ^ *(uint *)(iVar3 + 0x98)) - uVar1) + -1)
                                            >> 0x1f & 0x45909cU) + 0x35ce9a);
        uVar6 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),_opd_FUN_00902638);
        _opd_FUN_00245968(*(undefined4 *)(iVar3 + 0x60),uVar6,uVar5);
        uVar6 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0xac989c);
        _opd_FUN_00245968(*(undefined4 *)(iVar3 + 0x60),uVar6,uVar5);
      }
      piVar7 = (int *)_opd_FUN_00f30388();
      uVar5 = (*(code *)**(undefined4 **)(*piVar7 + 0x18))(piVar7,*(undefined4 *)(puVar2 + -0x7fe8))
      ;
      iVar8 = _opd_FUN_0023e4a0(0xf8a72d,0x4ac43d);
      if (iVar8 == 0) {
        uVar5 = _opd_FUN_00d734c0(uVar5,0,0);
        _opd_FUN_00228b78(auStack_90,*(undefined4 *)(puVar2 + -0x7fe4),
                          *(undefined4 *)(puVar2 + -0x7fe0),uVar5);
      }
      else {
        uVar5 = _opd_FUN_00d734c0(uVar5,0,0);
        _opd_FUN_00228b78(auStack_90,*(undefined4 *)(puVar2 + -0x7fe4),iVar8,uVar5);
      }
      uVar5 = _opd_FUN_00242bf0(*(undefined4 *)(iVar3 + 0x60),0x5807fd);
      _opd_FUN_00245968(*(undefined4 *)(iVar3 + 0x60),uVar5,auStack_90);
      if ((*(uint *)(**(int **)(puVar2 + -0x7fdc) + 8) & 8) != 0) {
        *(undefined4 *)(iVar3 + 0xa0) = 1;
      }
    }
  }
  return lVar9;
}

