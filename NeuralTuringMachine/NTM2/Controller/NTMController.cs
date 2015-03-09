﻿using System;
using NTM2.Memory;
using NTM2.Memory.Addressing;

namespace NTM2.Controller
{
    public class NTMController
    {
        private readonly UnitFactory _unitFactory;
        
        private readonly int _memoryColumnsN;
        private readonly int _memoryRowsM;
        private readonly int _weightsCount;
        private readonly double[] _input;
        private readonly ReadData[] _reads;
        private readonly NTMMemory _memory;
        
        private readonly IController _controller;

        //Old similarities
        private readonly BetaSimilarity[][] _wtm1s;

        public int WeightsCount
        {
            get { return _weightsCount; }
        }

        public int HeadCount
        {
            get { return ((FeedForwardController)_controller).OutputLayer._heads.Length; }
        }

        public Head[] Heads
        {
            get { return ((FeedForwardController)_controller).OutputLayer._heads; }
        }

        public Unit[] Output
        {
            get { return ((FeedForwardController)_controller).OutputLayer._outputLayer; }
        }

        public NTMController(int inputSize, int outputSize, int controllerSize, int headCount, int memoryColumnsN, int memoryRowsM)
        {
            _unitFactory = new UnitFactory();
            _memoryColumnsN = memoryColumnsN;
            _memoryRowsM = memoryRowsM;
            int headUnitSize = Head.GetUnitSize(memoryRowsM);
            _wtm1s = BetaSimilarity.GetTensor2(headCount, memoryColumnsN);
            _memory = new NTMMemory(memoryColumnsN, memoryRowsM, _unitFactory);
            
            _controller = new FeedForwardController(controllerSize, inputSize, outputSize, headCount, memoryRowsM, _unitFactory);
            
            _weightsCount =
                (headCount * memoryColumnsN) +
                (memoryColumnsN * memoryRowsM) +
                (controllerSize * headCount * memoryRowsM) +
                (controllerSize * inputSize) +
                (controllerSize) +
                (outputSize * (controllerSize + 1)) +
                (headCount * headUnitSize * (controllerSize + 1));
        }

        private NTMController(
            int memoryColumnsN,
            int memoryRowsM,
            int weightsCount,
            ReadData[] readDatas,
            double[] input,
            IController controller,
            UnitFactory unitFactory)
        {
            _unitFactory = unitFactory;
            _memoryColumnsN = memoryColumnsN;
            _memoryRowsM = memoryRowsM;
            _weightsCount = weightsCount;
            _reads = readDatas;
            _input = input;
            _controller = controller;
        }

        public TrainableNTM[] ProcessAndUpdateErrors(double[][] input, double[][] knownOutput)
        {
            //FOREACH HEAD - SET WEIGHTS TO BIAS VALUES
            ContentAddressing[] contentAddressings = ContentAddressing.GetVector(HeadCount, i => _wtm1s[i], _unitFactory);

            HeadSetting[] oldSettings = HeadSetting.GetVector(HeadCount, i => new Tuple<int, ContentAddressing>(_memory.MemoryColumnsN, contentAddressings[i]), _unitFactory);
            ReadData[] readDatas = ReadData.GetVector(HeadCount, i => new Tuple<HeadSetting, NTMMemory>(oldSettings[i], _memory));

            TrainableNTM[] machines = new TrainableNTM[input.Length];
            TrainableNTM empty = new TrainableNTM(this, new MemoryState(oldSettings, readDatas, _memory));

            //BPTT
            machines[0] = new TrainableNTM(empty, input[0], _unitFactory);
            for (int i = 1; i < input.Length; i++)
            {
                machines[i] = new TrainableNTM(machines[i - 1], input[i], _unitFactory);
            }

            UpdateWeights(unit => unit.Gradient = 0);

            for (int i = input.Length - 1; i >= 0; i--)
            {
                TrainableNTM machine = machines[i];
                double[] output = knownOutput[i];

                for (int j = 0; j < output.Length; j++)
                {
                    //Delta
                    ((FeedForwardController)(machine.Controller._controller)).OutputLayer._outputLayer[j].Gradient = ((FeedForwardController)(machine.Controller._controller)).OutputLayer._outputLayer[j].Value - output[j];
                }
                machine.BackwardErrorPropagation();
            }

            //Compute gradients for the bias values of internal memory and weights
            for (int i = 0; i < readDatas.Length; i++)
            {
                readDatas[i].BackwardErrorPropagation();
                for (int j = 0; j < readDatas[i].HeadSetting.Data.Length; j++)
                {
                    contentAddressings[i].Data[j].Gradient += readDatas[i].HeadSetting.Data[j].Gradient;
                }
                contentAddressings[i].BackwardErrorPropagation();
            }

            return machines;
        }

        public NTMController Process(ReadData[] readData, double[] input)
        {
            NTMController newController = new NTMController(
                _memoryColumnsN,
                _memoryRowsM,
                _weightsCount,
                readData,
                input,
                _controller.Clone(),
                _unitFactory);

            newController.ForwardPropagation(readData, input);
            return newController;
        }
        
        private void ForwardPropagation(ReadData[] readData, double[] input)
        {
            _controller.ForwardPropagation(input, readData);
        }

        public void UpdateWeights(Action<Unit> updateAction)
        {
            foreach (BetaSimilarity[] betaSimilarities in _wtm1s)
            {
                foreach (BetaSimilarity betaSimilarity in betaSimilarities)
                {
                    updateAction(betaSimilarity.Data);
                }
            }

            Action<Unit[][]> tensor2UpdateAction = Unit.GetTensor2UpdateAction(updateAction);

            tensor2UpdateAction(_memory.Data);
            
            _controller.UpdateWeights(updateAction);
        }
        
        public void BackwardErrorPropagation()
        {
            _controller.BackwardErrorPropagation(_input, _reads);
        }
    }
}
