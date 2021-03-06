﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Appleseed.DecisionTree
{
    [Serializable]
    public class DecisionTree
    {
        private TreeNode root;

        /// <summary>
        /// List of all attributes
        /// </summary>
        private List<int> attributes;

        /// <summary>
        /// dictionary that maps attribute names to a list of values that it can contain
        /// </summary>
        private Dictionary<int, HashSet<object>> attributeValues;

        /// <summary>
        /// classifies an arbitrary example using a built tree.
        /// 
        /// THE TREE MUST BE BUILT BEFORE CALLING THIS METHOD!
        /// </summary>
        public ClassifyOutput Classify(Example example)
        {
            TreeNode currentNode = root;

            double counts = 0;
            double randomizedCounts = 0;

            // while we aren't at a leaf node in the tree...
            while (!currentNode.terminal)
            {
                counts++;

                // look through the list of possible values that current node's
                // splitting attribute can take on.
                bool foundChild = false;
                
                foreach (var child in currentNode.children)
                {
                    if (child.value.Equals(example.attributes[currentNode.attribute]))
                    {
                        foundChild = true;
                        currentNode = child;
                        break;
                    }
                }
                
                if (!foundChild)
                {
                    randomizedCounts++;
                    Console.WriteLine("Choosing random path...");
                    Random r = new Random();
                    currentNode = currentNode.children[r.Next(currentNode.children.Count)];
                }
            }
            ClassifyOutput output;
            return output = new ClassifyOutput(currentNode.classification, randomizedCounts / counts);
            // return currentNode.classification;

        }

        public void BuildTree(List<Example> trainingSet)
        {
            attributes = new List<int>();
            attributeValues = new Dictionary<int, HashSet<object>>();


            // for each example in the training set...
            foreach (var trainingExample in trainingSet)
            {
                // for each attribute of that example...
                foreach (var attr in trainingExample.attributes)
                {
                    // if the tree's list of attributes doesn't contain the
                    // current attribute, add it.
                    if (!attributes.Contains(attr.Key))
                    {
                        attributes.Add(attr.Key);
                        attributeValues[attr.Key] = new HashSet<object>();
                    }

                    // if the tree's list of values doesn't contain the
                    // current value, add it.
                    if (!attributeValues[attr.Key].Contains(attr.Value))
                    {
                        attributeValues[attr.Key].Add(attr.Value);
                    }
                }
            }

            root = Learn(trainingSet, attributes, "", null, null);

            Test(trainingSet);
        }
        // private DecTreeNode DecisionTreeLearning(List<Instance> examples, List<String> currentAttrs, String defaultClassifier, List<Instance> parentExamples, DecTreeNode parentNode) {

        private TreeNode Learn(List<Example> examples, List<int> currentAttrs, string defaultClassification, List<Example> parentExamples, TreeNode parentNode)
        {

            // Check if there are no more examples to be classified
            if (examples.Count == 0)
            {
                return new TreeNode(MajorityClassify(parentExamples), -1, parentNode.value, true);
            }

            // check if all the remaining examples have the same classification
            bool allHaveSameClassification = true;
            string classification = examples[0].classification;
            foreach (var ex in examples)
            {
                if (!ex.classification.Equals(classification))
                {
                    allHaveSameClassification = false;
                    break;
                }
            }
            if (allHaveSameClassification)
            {
                return new TreeNode(classification, -1, parentNode.value, true);
            }

            // check if there are no more attributes to split on
            if (currentAttrs.Count == 0)
            {
                return new TreeNode(MajorityClassify(examples), -1, parentNode.value, true);
            }

            // find the attribute that results in the highest 
            // information gain for the current node
            double highestInfoGain = Double.NegativeInfinity; // initialized to really low number
            int highestAttr = -1;
            foreach(int attr in currentAttrs)
            {
                double gain = InfoGain(examples, attr);
                if (gain > highestInfoGain)
                {
                    highestInfoGain = gain;
                    highestAttr = attr;
                }
            }

            object pAV = null;
            if (parentNode != null)
            {
                pAV = parentNode.value;
            }

            // create the tree that splits on the best found attribute
            TreeNode tree = new TreeNode(null, highestAttr, pAV, false);

            // for each possible value that the highest attribute can take on...
            foreach (var value in attributeValues[highestAttr])
            {
                List<Example> exs = new List<Example>();
                // for each example remaining...
                foreach (Example ex in examples)
                {
                    // if the appropriate attribute for the example is equal to
                    // the current possible example...
                    if (ex.attributes[highestAttr].Equals(value))
                    {
                        // add it to the list
                        exs.Add(ex);
                    }
                }

                List<int> newAttrs = new List<int>(currentAttrs);
                newAttrs.Remove(highestAttr);

                TreeNode childTree = new TreeNode(null, highestAttr, value, false);
                tree.AddChild(Learn(exs, newAttrs, MajorityClassify(exs), examples, childTree));
            }
            return tree;
        }

        private Dictionary<string, int> CountLabelOccurrences(List<Example> examples)
        {
            Dictionary<string, int> occurrences = new Dictionary<string, int>();
            foreach (var example in examples)
            {
                if (occurrences.ContainsKey(example.classification)) {
                    occurrences[example.classification]++;
                } else
                {
                    occurrences[example.classification] = 1;
                }
            }

            return occurrences;
        }

        private double InfoGain(List<Example> examples, int attr)
        {
            return Entropy(examples) - ConditionalEntropy(examples, attr);
        }

        private double Entropy(List<Example> examples)
        {
            int examplesSize = examples.Count;

            double entropy = 0.0;

            Dictionary<string, int> occurrences = CountLabelOccurrences(examples);

            foreach (string treeClass in occurrences.Keys)
            {
                double probability = (double)(occurrences[treeClass]) / (double)(examplesSize);
                entropy += -probability * Math.Log(probability, 2);
            }

            return entropy;
        }

        private double ConditionalEntropy(List<Example> examples, int attr)
        {
            HashSet<object> attrValues = new HashSet<object>(attributeValues[attr]);

            // counts the number of times each value that attr can take on appears
            Dictionary<object, int> valuesCount = new Dictionary<object, int>();

            Dictionary<object, double> specificCondEntropy = new Dictionary<object, double>();

            foreach(object value in attrValues)
            {
                valuesCount[value] = 0;
                specificCondEntropy[value] = 0.0;
            }

            // count the values
            foreach (Example ex in examples)
            {
                object currentAttr = ex.attributes[attr];

                valuesCount[currentAttr] = valuesCount[currentAttr] + 1;
            }

            // total number of classifications in instances
            int classificationCount = 0;
            foreach (int i in valuesCount.Values)
            {
                classificationCount += i;
            }

            foreach (object value in attrValues)
            {
                Dictionary<string, int> classificationOccurrences = CountClassificationOccurrencesGivenAttr(examples, attr, value);
                int totalClassificationCountForVal = 0;

                foreach (int i in classificationOccurrences.Values)
                {
                    totalClassificationCountForVal += i;
                }

                // for each possible relevant classification...
                foreach (string classification in classificationOccurrences.Keys)
                {
                    double fraction = (double)(classificationOccurrences[classification]) /
                                      (double)totalClassificationCountForVal;

                    specificCondEntropy[value] = specificCondEntropy[value] 
                        + -(fraction * Math.Log(fraction, 2));
                }
            }

            double condH = 0.0;
            foreach (object value in attrValues)
            {
                double probability = (double)(valuesCount[value]) / (double)(classificationCount);
                double sCondH = specificCondEntropy[value];

                condH += probability * sCondH;
            }

            return condH;
        }

        private Dictionary<string, int> CountClassificationOccurrencesGivenAttr(List<Example> examples, int attr, object value)
        {
            Dictionary<string, int> occurrences = new Dictionary<string, int>();
            foreach (Example ex in examples)
            {
                var currentValue = ex.attributes[attr];

                if (currentValue.Equals(value))
                {
                    if (occurrences.ContainsKey(ex.classification))
                    {
                        occurrences[ex.classification] = occurrences[ex.classification] + 1;
                    } else
                    {
                        occurrences[ex.classification] = 1;
                    }
                }
            }

            return occurrences;
        }

        private string MajorityClassify(List<Example> Examples)
        {
            Dictionary<string, int> map = new Dictionary<string, int>();
            foreach (Example ex in Examples)
            {
                if (map.ContainsKey(ex.classification))
                {
                    // if already in the map, increment the count
                    map[ex.classification] = map[ex.classification] + 1;
                } else
                {
                    map[ex.classification] = 1;
                }
            }

            int highestCount = -1;
            string majorityString = null;

            foreach (string key in map.Keys)
            {
                if (map[key] > highestCount)
                {
                    majorityString = key;
                    highestCount = map[key];
                }
            }
            return majorityString;
        }

        public void serialize(string path)
        {
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream(
                path + ".bin",
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            formatter.Serialize(stream, this);
            stream.Close();
        }

        public static DecisionTree deserialize(string path)
        {
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read);

            DecisionTree tree = (DecisionTree)formatter.Deserialize(stream);
            stream.Close();
            return tree;
        } 

        public void Test(List<Example> examples)
        {
            double count = 0;
            double correct = 0;
            foreach (var ex in examples)
            {
                count++;


                // if the current example's given classification is equal to the "guess"
                if (Classify(ex).classification.Equals(ex.classification))
                {
                    correct++;
                } 
            }

            Console.WriteLine("Correct: " + (correct / count) * 100 + "%");
        }
    }
}
