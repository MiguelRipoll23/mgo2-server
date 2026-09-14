/* containing function of static patch target(s); entry .opd.FUN_00286358 @ 00286358 */

void _opd_FUN_00286358(int *param_1)

{
  undefined4 *puVar1;
  uint uVar2;
  undefined *puVar3;
  undefined4 uVar5;
  undefined8 uVar4;
  int iVar6;
  int iVar7;
  uint uVar8;
  undefined1 auStack_190 [8];
  undefined1 auStack_188 [32];
  undefined1 auStack_168 [12];
  undefined1 auStack_15c [268];
  
  puVar3 = PTR_PTR_0121af98;
  uVar5 = _opd_FUN_0026d350(0x2400,2);
  _opd_FUN_00263c70(param_1,uVar5,0x400,0,0x2000);
  *param_1 = *(int *)(puVar3 + -0x7ffc);
  *(undefined1 *)((int)param_1 + 0x56) = 1;
  param_1[0x11] = 0;
  *(undefined2 *)(param_1 + 5) = 0;
  param_1[0x1e] = 0;
  *(undefined1 *)((int)param_1 + 5) = 8;
  *(undefined2 *)(param_1 + 0x10) = 0;
  *(undefined2 *)((int)param_1 + 0x42) = 0;
  _opd_FUN_0026c9e0(param_1 + 0x1c);
  param_1[0x22] = 0x708;
  param_1[10] = 0;
  param_1[6] = 0;
  param_1[7] = 0;
  param_1[8] = 0;
  param_1[9] = 0;
  *(undefined2 *)(param_1 + 0xc) = *(undefined2 *)(param_1 + 9);
  param_1[0xb] = param_1[8];
  *(undefined2 *)((int)param_1 + 6) = 0;
  *(undefined1 *)((int)param_1 + 0x16) = 0;
  param_1[0x12] = -1;
  _opd_FUN_00269808(param_1[0xd]);
  _opd_FUN_00269808(param_1[0xf]);
  _opd_FUN_0026c9e0(param_1 + 0x16);
  param_1[0x24] = 0x1f400;
  param_1[0x27] = 0;
  param_1[0x28] = 0;
  param_1[0x23] = 0x1f400;
  *(undefined2 *)((int)param_1 + 0x52) = 0;
  *(undefined2 *)(param_1 + 0x14) = 0;
  param_1[0x21] = 0;
  param_1[0x1f] = -1;
  *(undefined2 *)((int)param_1 + 0xc2) = 0xffff;
  *(undefined2 *)(param_1 + 0x31) = 0xffff;
  *(undefined2 *)((int)param_1 + 0xc6) = 0xffff;
  *(undefined2 *)(param_1 + 0x32) = 0xffff;
  _opd_FUN_0026ca78(param_1 + 0x16,param_1 + 0x2a);
  *(undefined2 *)((int)param_1 + 0xb6) = 0;
  *(undefined2 *)(param_1 + 0x2d) = 0;
  *(undefined2 *)((int)param_1 + 0xb2) = 0;
  *(undefined2 *)(param_1 + 0x2c) = 0;
  *(undefined1 *)((int)param_1 + 0xcd) = 0;
  *(undefined1 *)((int)param_1 + 0xca) = 0;
  *(undefined1 *)((int)param_1 + 0xcb) = 0;
  *(undefined1 *)(param_1 + 0x33) = 0;
  *(undefined1 *)((int)param_1 + 0xd1) = 0;
  *(undefined1 *)((int)param_1 + 0xce) = 0;
  *(undefined1 *)((int)param_1 + 0xcf) = 0;
  *(undefined1 *)(param_1 + 0x34) = 0;
  param_1[4] = 0;
  *(ushort *)(param_1 + 5) = *(ushort *)(param_1 + 5) | 7;
  param_1[2] = 0;
  *(undefined1 *)(param_1 + 1) = 8;
  _opd_FUN_0026c9e0(param_1 + 0x1b6);
  puVar1 = *(undefined4 **)(puVar3 + -0x8000);
  uVar4 = (**(code **)**(undefined4 **)*puVar1)
                    ((undefined4 *)*puVar1,*(undefined4 *)(puVar3 + -0x7ff4),0);
  param_1[0x23b] = (int)uVar4;
  iVar6 = (*(code *)**(undefined4 **)(*(int *)*puVar1 + 8))((int *)*puVar1,uVar4,auStack_190,4);
  if ((iVar6 == 4) &&
     (iVar6 = _opd_FUN_00fa4e20(auStack_190,*(undefined4 *)(puVar3 + -0x7ff0),4), iVar6 == 0)) {
    (*(code *)**(undefined4 **)(*(int *)*puVar1 + 8))
              ((int *)*puVar1,param_1[0x23b],*(undefined4 *)(puVar3 + -0x7fec),4);
    (*(code *)**(undefined4 **)(*(int *)*puVar1 + 8))
              ((int *)*puVar1,param_1[0x23b],*(undefined4 *)(puVar3 + -0x7fe8),4);
    if (-1 < param_1[0x23b]) {
      iVar6 = (*(code *)**(undefined4 **)(*(int *)*puVar1 + 8))
                        ((int *)*puVar1,param_1[0x23b],auStack_188,9);
      if (5 < iVar6) {
        _opd_FUN_0026cad8(auStack_168,auStack_15c,0x100);
        _opd_FUN_0026caf0(auStack_168,auStack_188,iVar6);
        _opd_FUN_0026cc88(auStack_168,param_1 + 0x1b8,4);
        _opd_FUN_0026cc88(auStack_168,param_1 + 0x1b9,2);
        uVar2 = *(ushort *)(param_1 + 0x1b9) & 0xfff;
        if (uVar2 < 0x200) {
          if ((short)*(ushort *)(param_1 + 0x1b9) < 0) {
            _opd_FUN_0026cc88(auStack_168,(int)param_1 + 0x6e6,2);
            _opd_FUN_0026cc88(auStack_168,param_1 + 0x1ba,1);
            uVar8 = (*(code *)**(undefined4 **)(*(int *)*puVar1 + 8))
                              ((int *)*puVar1,param_1[0x23b],(int)param_1 + 0x6eb,uVar2);
            if (uVar2 == uVar8) {
              return;
            }
          }
          else {
            iVar6 = _opd_FUN_0026cb20(auStack_168,(int)param_1 + 0x6eb,3);
            iVar7 = (*(code *)**(undefined4 **)(*(int *)*puVar1 + 8))
                              ((int *)*puVar1,param_1[0x23b],(int)param_1 + iVar6 + 0x6eb,
                               uVar2 - iVar6);
            if (uVar2 - iVar6 == iVar7) {
              return;
            }
          }
        }
      }
      (*(code *)**(undefined4 **)(*(int *)*puVar1 + 4))((int *)*puVar1,param_1[0x23b]);
      *(undefined2 *)(param_1 + 0x1b9) = 0;
      param_1[0x23b] = -1;
      (*(code *)**(undefined4 **)(*param_1 + 8))(param_1);
    }
  }
  else {
    (*(code *)**(undefined4 **)(*(int *)*puVar1 + 4))((int *)*puVar1,param_1[0x23b]);
    param_1[0x23b] = -1;
  }
  return;
}

